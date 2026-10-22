using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application;
using Retail.Application.DTOs.Auth;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Restauración de punta a punta contra SQL Server Express (RF-22, RF-23, D-17 a D-26): copia real con <c>IBackupService</c>,
/// restore real con <c>IRestauracionService</c> y la inyección de dependencias de producción. Cada operación corre en
/// su propio scope, como lo haría la aplicación, porque tras un restore el contexto viejo apunta a una base reemplazada.
/// </summary>
public class RestauracionIntegracionTests : IAsyncLifetime
{
    private const string NombreGerente = "gerente";
    private const string ClaveGerente = "Clave2026!";
    private const string ClaveAdministrador = "ClaveAdmin2026!";

    private readonly string _vigente = $"RetailDb_Test_Restauracion_{Guid.NewGuid():N}";
    private readonly string _directorio = ServidorDePruebas.CrearCarpetaAccesibleParaElMotor("RetailRestauracionTests");

    // Carpeta común del usuario (como Descargas), SIN permisos para la cuenta del motor: B-3.
    private readonly string _externa = Path.Combine(Path.GetTempPath(), $"RetailRestauracionExterna_{Guid.NewGuid():N}");
    private readonly CompuertaDeCopia _compuerta = new();
    private ServiceProvider _servicios = null!;
    private int _idGerente;

    /// <summary>
    /// Punto de control de una copia en vuelo: cuando está armada, la copia avisa que empezó y espera la señal para seguir.
    /// </summary>
    private sealed class CompuertaDeCopia
    {
        private readonly TaskCompletionSource _entro = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _continuar = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Armada { get; set; }

        public Task Entro => _entro.Task;

        public void AvisarQueEntro() => _entro.TrySetResult();

        public Task EsperarSenalAsync() => _continuar.Task;

        public void Liberar() => _continuar.TrySetResult();
    }

    private sealed class EjecutorConCompuerta : IEjecutorBackupBaseDatos
    {
        private readonly IEjecutorBackupBaseDatos _real;
        private readonly CompuertaDeCopia _compuerta;

        public EjecutorConCompuerta(IEjecutorBackupBaseDatos real, CompuertaDeCopia compuerta)
        {
            _real = real;
            _compuerta = compuerta;
        }

        public async Task<ArchivoBackupDto> CrearCopiaAsync(string rutaArchivo, IProgress<int>? progreso, CancellationToken cancellationToken = default)
        {
            if (_compuerta.Armada)
            {
                _compuerta.Armada = false;
                _compuerta.AvisarQueEntro();
                await _compuerta.EsperarSenalAsync();
            }

            return await _real.CrearCopiaAsync(rutaArchivo, progreso, cancellationToken);
        }

        public Task VerificarCopiaAsync(string rutaArchivo, CancellationToken cancellationToken = default)
        {
            return _real.VerificarCopiaAsync(rutaArchivo, cancellationToken);
        }
    }

    private static string CadenaDe(string baseDeDatos) => ServidorDePruebas.CadenaDeConexion(baseDeDatos);

    private string CadenaVigente => CadenaDe(_vigente);

    private static RetailDbContext ContextoDe(string cadena)
    {
        return new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(cadena).Options);
    }

    public Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = CadenaVigente,
                ["ArcaSettings:UseMockArca"] = "true",
                ["BackupSettings:Directorio"] = _directorio,
                ["BackupSettings:CantidadMaximaCopias"] = "14",
                ["BackupSettings:EspacioLibreMinimoMb"] = "1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddAdministracionSistema();
        services.AddInfrastructureServices(configuracion);

        // Mismo ejecutor real, pero una prueba puede detenerlo a mitad de la copia (con la copia viva y su bloqueo tomado).
        services.AddScoped<IEjecutorBackupBaseDatos>(sp =>
            new EjecutorConCompuerta(ActivatorUtilities.CreateInstance<SqlServerEjecutorBackup>(sp), _compuerta));
        _servicios = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _servicios.DisposeAsync();
        SqlConnection.ClearAllPools();

        // Se limpia aunque la prueba haya fallado a mitad de camino: la vigente, la temporal y todas las anteriores.
        await using (var master = new SqlConnection(CadenaDe("master")))
        {
            await master.OpenAsync();

            var nombres = new List<string> { _vigente, $"{_vigente}_Restaurando" };
            nombres.AddRange(await ListarBasesAnterioresAsync());

            foreach (var nombre in nombres)
            {
                await using var comando = master.CreateCommand();
                var delimitado = $"[{nombre.Replace("]", "]]", StringComparison.Ordinal)}]";
                comando.CommandText =
                    $"IF DB_ID(@nombre) IS NOT NULL BEGIN " +
                    $"BEGIN TRY ALTER DATABASE {delimitado} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; END TRY BEGIN CATCH END CATCH; " +
                    $"DROP DATABASE {delimitado}; END";
                comando.Parameters.AddWithValue("@nombre", nombre);
                await comando.ExecuteNonQueryAsync();
            }
        }

        SqlConnection.ClearAllPools();

        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }

        if (Directory.Exists(_externa))
        {
            Directory.Delete(_externa, recursive: true);
        }
    }

    // ───────────────────── Ayudas ─────────────────────

    /// <summary>
    /// Ejecuta una operación en un scope nuevo, como una pantalla de la aplicación o una ejecución de la consola.
    /// </summary>
    private async Task<T> EnScopeAsync<T>(Func<IServiceProvider, Task<T>> operacion)
    {
        await using var scope = _servicios.CreateAsyncScope();
        return await operacion(scope.ServiceProvider);
    }

    private Task<ResultadoBackupDto> CrearBackupManualAsync()
    {
        return EnScopeAsync(sp => sp.GetRequiredService<IBackupService>().CrearBackupManualAsync(_idGerente, ClaveGerente));
    }

    private Task<ResultadoRestauracionDto> RestaurarDesdeHistorialAsync(Guid referencia, string password = ClaveGerente)
    {
        return EnScopeAsync(sp => sp.GetRequiredService<IRestauracionService>()
            .RestaurarDesdeHistorialAsync(_idGerente, password, referencia));
    }

    private Task<ResultadoRestauracionDto> RestaurarDesdeArchivoAsync(string ruta, string password = ClaveGerente)
    {
        return EnScopeAsync(sp => sp.GetRequiredService<IRestauracionService>()
            .RestaurarDesdeArchivoAsync(ruta, NombreGerente, password));
    }

    /// <summary>
    /// Migra la base vigente y da de alta un Gerente con contraseña propia (<c>DebeCambiarPassword</c> en falso) y un
    /// hash BCrypt real, como exige la verificación dentro de la copia (D-18).
    /// </summary>
    private async Task PrepararBaseConGerenteAsync()
    {
        await using var scope = _servicios.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await using var contexto = ContextoDe(CadenaVigente);
        await contexto.Database.MigrateAsync();

        var gerente = Usuario.Crear(
            NombreGerente, "Ana", "Perez", hasher.HashPassword("provisoria"), RolUsuarioEnum.Gerente, "ana@example.com");
        gerente.CambiarPasswordPropia(hasher.HashPassword(ClaveGerente));
        contexto.Usuarios.Add(gerente);
        await contexto.SaveChangesAsync();

        _idGerente = gerente.Id;
    }

    private static async Task AgregarCategoriaAsync(string cadena, string nombre)
    {
        await using var contexto = ContextoDe(cadena);
        var rubro = Rubro.Crear($"Rubro de {nombre}");
        contexto.Rubros.Add(rubro);
        await contexto.SaveChangesAsync();

        contexto.Categorias.Add(Categoria.Crear(nombre, rubro.Id));
        await contexto.SaveChangesAsync();
    }

    private static async Task<List<string>> CategoriasDeAsync(string baseDeDatos)
    {
        await using var contexto = ContextoDe(CadenaDe(baseDeDatos));
        return await contexto.Categorias.AsNoTracking().Select(c => c.NombreCategoria).ToListAsync();
    }

    private static async Task<bool> ExisteBaseAsync(string nombre)
    {
        await using var conexion = new SqlConnection(CadenaDe("master"));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT DB_ID(@nombre);";
        comando.Parameters.AddWithValue("@nombre", nombre);
        var resultado = await comando.ExecuteScalarAsync();
        return resultado is not null and not DBNull;
    }

    /// <summary>
    /// Bases <c>{vigente}_Anterior_*</c>. El guion bajo es comodín de LIKE: se escapa para no mezclar bases ajenas.
    /// </summary>
    private async Task<List<string>> ListarBasesAnterioresAsync()
    {
        var nombres = new List<string>();
        await using var master = new SqlConnection(CadenaDe("master"));
        await master.OpenAsync();
        await using var comando = master.CreateCommand();
        comando.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @patron ESCAPE '\\';";
        comando.Parameters.AddWithValue("@patron", _vigente.Replace("_", "\\_", StringComparison.Ordinal) + "\\_Anterior\\_%");
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            nombres.Add(lector.GetString(0));
        }

        return nombres;
    }

    private static void CorromperArchivo(string ruta)
    {
        var bytes = File.ReadAllBytes(ruta);
        for (var i = bytes.Length / 2; i < (bytes.Length / 2) + 64; i++)
        {
            bytes[i] ^= 0xFF;
        }

        File.WriteAllBytes(ruta, bytes);
    }

    private async Task EliminarVigenteAsync()
    {
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(CadenaDe("master"));
        await master.OpenAsync();
        await using var comando = master.CreateCommand();
        comando.CommandText = $"ALTER DATABASE [{_vigente}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_vigente}];";
        await comando.ExecuteNonQueryAsync();
        SqlConnection.ClearAllPools();
    }

    /// <summary>
    /// Escenario común: datos A, copia manual, y luego datos B en la vigente.
    /// </summary>
    private async Task<ResultadoBackupDto> PrepararCopiaConDatosAYCambiarABAsync()
    {
        await PrepararBaseConGerenteAsync();
        await AgregarCategoriaAsync(CadenaVigente, "Datos A");
        var copia = await CrearBackupManualAsync();
        await AgregarCategoriaAsync(CadenaVigente, "Datos B");
        return copia;
    }

    // ───────────────────── Caminos felices ─────────────────────

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_CopiaVerificada_DejaLosDatosDeLaCopiaYConservaLaBaseAnterior()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();

        // Act
        var resultado = await RestaurarDesdeHistorialAsync(copia.Referencia);

        // Assert: la vigente tiene los datos de la copia; la anterior conserva los cambios posteriores.
        (await CategoriasDeAsync(_vigente)).Should().ContainSingle().Which.Should().Be("Datos A");
        resultado.BaseAnterior.Should().NotBeNullOrWhiteSpace();
        (await ExisteBaseAsync(resultado.BaseAnterior!)).Should().BeTrue();
        (await CategoriasDeAsync(resultado.BaseAnterior!)).Should().BeEquivalentTo("Datos A", "Datos B");
        (await ExisteBaseAsync($"{_vigente}_Restaurando")).Should().BeFalse("la temporal pasó a ser la vigente");

        resultado.ReferenciaCopia.Should().Be(copia.Referencia);
        resultado.ReferenciaCopiaPrevia.Should().NotBeNull();
        resultado.Advertencias.Should().NotBeEmpty();
        resultado.Advertencias[0].Should().Be(ResultadoRestauracionDto.AvisoConciliacionArca);

        await using var contexto = ContextoDe(CadenaVigente);

        // R-18: dentro del .bak la copia restaurada estaba EnCurso; al restaurarla se cierra como Verificado.
        var registros = await contexto.RegistrosBackup.AsNoTracking().ToListAsync();
        registros.Single(r => r.Referencia == copia.Referencia).Estado.Should().Be(EstadoBackupEnum.Verificado);

        var previa = registros.Single(r => r.Referencia == resultado.ReferenciaCopiaPrevia);
        previa.Estado.Should().Be(EstadoBackupEnum.Verificado);
        previa.Tipo.Should().Be(TipoBackupEnum.PreRestauracion);
        registros.Should().NotContain(r => r.Estado == EstadoBackupEnum.EnCurso);

        var restauraciones = await contexto.RegistrosRestauracion.AsNoTracking().ToListAsync();
        var registro = restauraciones.Should().ContainSingle().Subject;
        registro.Referencia.Should().Be(resultado.ReferenciaRestauracion);
        registro.Origen.Should().Be(OrigenRestauracionEnum.Aplicacion);
        registro.IdUsuario.Should().Be(_idGerente);
        registro.NombreUsuario.Should().Be(NombreGerente);
        registro.ReferenciaCopia.Should().Be(copia.Referencia);
        registro.ReferenciaCopiaPrevia.Should().Be(resultado.ReferenciaCopiaPrevia);
        registro.BaseAnterior.Should().Be(resultado.BaseAnterior);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_CredencialDeGerenteDeLaCopia_RestauraConOrigenConsola()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();

        // Act
        var resultado = await RestaurarDesdeArchivoAsync(copia.RutaArchivo);

        // Assert
        (await CategoriasDeAsync(_vigente)).Should().ContainSingle().Which.Should().Be("Datos A");
        (await CategoriasDeAsync(resultado.BaseAnterior!)).Should().BeEquivalentTo("Datos A", "Datos B");
        (await ExisteBaseAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        resultado.Advertencias[0].Should().Be(ResultadoRestauracionDto.AvisoConciliacionArca);

        await using var contexto = ContextoDe(CadenaVigente);
        var gerenteEnLaCopia = await contexto.Usuarios.AsNoTracking().SingleAsync(u => u.NombreUsuario == NombreGerente);
        var registro = await contexto.RegistrosRestauracion.AsNoTracking().SingleAsync();
        registro.Origen.Should().Be(OrigenRestauracionEnum.Consola);
        registro.IdUsuario.Should().Be(gerenteEnLaCopia.Id);
        registro.ReferenciaCopia.Should().Be(copia.Referencia);
        registro.ReferenciaCopiaPrevia.Should().Be(resultado.ReferenciaCopiaPrevia);
        registro.BaseAnterior.Should().Be(resultado.BaseAnterior);
    }

    // ───────────────────── Control dual (D-30, D-31) ─────────────────────

    private const string NombreAdministrador = "soporte";

    /// <summary>
    /// Da de alta en la vigente un Administrador con contraseña propia y hash BCrypt real.
    /// </summary>
    private async Task<int> CrearAdministradorAsync()
    {
        await using var scope = _servicios.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await using var contexto = ContextoDe(CadenaVigente);
        var administrador = Usuario.Crear(
            NombreAdministrador, "Sara", "Ruiz", hasher.HashPassword("provisoria"), RolUsuarioEnum.Administrador, null);
        administrador.CambiarPasswordPropia(hasher.HashPassword(ClaveAdministrador));
        contexto.Usuarios.Add(administrador);
        await contexto.SaveChangesAsync();
        return administrador.Id;
    }

    [Fact]
    public async Task RestaurarConAutorizacionDeGerenteAsync_PasswordIncorrecta_PersisteElIntentoEnLaBase()
    {
        // Arrange
        await PrepararBaseConGerenteAsync();
        var idAdministrador = await CrearAdministradorAsync();
        var copia = await CrearBackupManualAsync();

        // Act
        var act = () => EnScopeAsync(sp => sp.GetRequiredService<IRestauracionService>()
            .RestaurarConAutorizacionDeGerenteAsync(
                idAdministrador, ClaveAdministrador, new CredencialAutorizanteDto(NombreGerente, "ClaveIncorrecta1!"), copia.Referencia));

        // Assert: con un contexto nuevo se comprueba que el contador quedó guardado en la base, no solo en memoria.
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        await using var contexto = ContextoDe(CadenaVigente);
        var administrador = await contexto.Usuarios.AsNoTracking().SingleAsync(u => u.Id == idAdministrador);
        administrador.IntentosAutorizacionFallidos.Should().Be(1);
    }

    [Fact]
    public async Task RestaurarConAutorizacionDeGerenteAsync_CredencialValida_RegistraOperadorYAutorizante()
    {
        // Arrange
        await PrepararBaseConGerenteAsync();
        var idAdministrador = await CrearAdministradorAsync();
        var copia = await CrearBackupManualAsync();
        await AgregarCategoriaAsync(CadenaVigente, "Datos B");

        // Act
        var resultado = await EnScopeAsync(sp => sp.GetRequiredService<IRestauracionService>()
            .RestaurarConAutorizacionDeGerenteAsync(
                idAdministrador, ClaveAdministrador, new CredencialAutorizanteDto(NombreGerente, ClaveGerente), copia.Referencia));

        // Assert: en la base restaurada queda el Gerente que autorizó y el Administrador que ejecutó.
        await using var contexto = ContextoDe(CadenaVigente);
        var registro = await contexto.RegistrosRestauracion.AsNoTracking()
            .SingleAsync(r => r.Referencia == resultado.ReferenciaRestauracion);
        registro.IdUsuario.Should().Be(_idGerente);
        registro.NombreUsuario.Should().Be(NombreGerente);
        registro.IdOperador.Should().Be(idAdministrador);
        registro.NombreOperador.Should().Be(NombreAdministrador);
    }

    private sealed record FilaDeAuditoria(string Nombre, int? IdUsuario, byte Resultado, byte Tipo, string? Detalle);

    private async Task<List<FilaDeAuditoria>> AuditoriaDeLaVigenteAsync()
    {
        SqlConnection.ClearAllPools();
        var filas = new List<FilaDeAuditoria>();
        await using var conexion = new SqlConnection(CadenaVigente);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT nombre_usuario, id_usuario, resultado, tipo_evento, detalle FROM dbo.AUDITORIA_ACCESOS ORDER BY id_auditoria";
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            filas.Add(new FilaDeAuditoria(
                lector.GetString(0),
                lector.IsDBNull(1) ? null : lector.GetInt32(1),
                lector.GetByte(2),
                lector.GetByte(3),
                lector.IsDBNull(4) ? null : lector.GetString(4)));
        }

        return filas;
    }

    private async Task RegistrarLoginAsync(string nombreUsuario)
    {
        await EnScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IRegistroAccesosService>().RegistrarIntentoAsync(nombreUsuario, _idGerente, credencialesValidas: true);
            return 0;
        });
    }

    private Task<ResultadoRestauracionDto> RestaurarAutorizadoAsync(int idAdministrador, Guid referenciaCopia)
    {
        return EnScopeAsync(sp => sp.GetRequiredService<IRestauracionService>()
            .RestaurarConAutorizacionDeGerenteAsync(
                idAdministrador, ClaveAdministrador, new CredencialAutorizanteDto(NombreGerente, ClaveGerente), referenciaCopia));
    }

    [Fact]
    public async Task RestaurarConAutorizacionDeGerenteAsync_CredencialValida_ConservaLaAuditoriaDeLaVigente()
    {
        // Arrange: un login que la copia ya contiene y otro posterior a la copia, que solo existe en la vigente.
        await PrepararBaseConGerenteAsync();
        var idAdministrador = await CrearAdministradorAsync();
        await RegistrarLoginAsync("login-previo");
        var copia = await CrearBackupManualAsync();
        await RegistrarLoginAsync("login-posterior");

        // Act
        await RestaurarAutorizadoAsync(idAdministrador, copia.Referencia);

        // Assert: la auditoría sobrevive al restore y el hecho del propio restore quedó registrado.
        var filas = await AuditoriaDeLaVigenteAsync();
        filas.Should().ContainSingle(f => f.Nombre == "login-previo");

        var posterior = filas.Should().ContainSingle(f => f.Nombre == "login-posterior").Subject;
        posterior.IdUsuario.Should().BeNull("el Id de usuario puede ser otra persona en la base restaurada");
        posterior.Tipo.Should().Be((byte)TipoEventoAccesoEnum.InicioSesion);

        var autorizacion = filas.Should().ContainSingle(f => f.Tipo == (byte)TipoEventoAccesoEnum.AutorizacionRestauracion).Subject;
        autorizacion.Nombre.Should().Be(NombreAdministrador);
        autorizacion.IdUsuario.Should().BeNull();
        autorizacion.Resultado.Should().Be((byte)ResultadoEventoAccesoEnum.Aceptado);
        autorizacion.Detalle.Should().Be($"Gerente: {NombreGerente}");
    }

    [Fact]
    public async Task RestaurarConAutorizacionDeGerenteAsync_SegundoRestore_NoDuplicaLaAuditoria()
    {
        // Arrange
        await PrepararBaseConGerenteAsync();
        var idAdministrador = await CrearAdministradorAsync();
        await RegistrarLoginAsync("login-previo");
        var copia = await CrearBackupManualAsync();
        await RegistrarLoginAsync("login-posterior");
        await RestaurarAutorizadoAsync(idAdministrador, copia.Referencia);

        // Act: se restaura la misma copia otra vez, ahora sobre la base ya restaurada.
        await RestaurarAutorizadoAsync(idAdministrador, copia.Referencia);

        // Assert: cada hecho figura una sola vez; hay una autorización Aceptada por restore.
        var filas = await AuditoriaDeLaVigenteAsync();
        filas.Count(f => f.Nombre == "login-previo").Should().Be(1);
        filas.Count(f => f.Nombre == "login-posterior").Should().Be(1);
        filas.Count(f => f.Tipo == (byte)TipoEventoAccesoEnum.AutorizacionRestauracion).Should().Be(2);
    }

    // ───────────────────── B-3: copias fuera del directorio de respaldos ─────────────────────

    /// <summary>
    /// Lleva el .bak y su manifiesto a una carpeta sin permisos para el motor, como si el Gerente los hubiera bajado a
    /// Descargas desde un pendrive. Se COPIA y se borra el original en lugar de mover: un <c>File.Move</c> en el mismo
    /// volumen conserva la lista de acceso del archivo (con el permiso del motor, heredado del directorio de respaldos) y el
    /// motor podría leerlo igual; un archivo nuevo hereda los permisos de la carpeta externa, como en la vida real.
    /// </summary>
    private string MoverACarpetaExterna(string rutaBak)
    {
        Directory.CreateDirectory(_externa);
        var destino = Path.Combine(_externa, Path.GetFileName(rutaBak));
        File.Copy(rutaBak, destino);
        File.Copy(Path.ChangeExtension(rutaBak, ".json"), Path.ChangeExtension(destino, ".json"));
        File.Delete(rutaBak);
        File.Delete(Path.ChangeExtension(rutaBak, ".json"));
        return destino;
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_CopiaEnCarpetaSinPermisosParaElMotor_LaCopiaAlDirectorioYRestaura()
    {
        // Arrange: el motor (otra cuenta de Windows) no puede leer la carpeta externa.
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        var externa = MoverACarpetaExterna(copia.RutaArchivo);

        // Control negativo: sin la preparación, el motor realmente no puede leer esa carpeta. Si pudiera (otra cuenta de
        // servicio, otra configuración de la PC), esta prueba no demostraría nada y debe avisarlo en lugar de pasar en falso.
        await using (var master = new SqlConnection(CadenaDe("master")))
        {
            await master.OpenAsync();
            await using var verificacion = master.CreateCommand();
            verificacion.CommandText = "RESTORE VERIFYONLY FROM DISK = @ruta;";
            verificacion.Parameters.AddWithValue("@ruta", externa);

            var directa = () => verificacion.ExecuteNonQueryAsync();

            (await directa.Should().ThrowAsync<SqlException>("el motor corre con otra cuenta que no tiene permiso sobre la carpeta externa"))
                .Which.Message.Should().Contain("error 5", "5 es ERROR_ACCESS_DENIED de Windows, en cualquier idioma");
        }

        // Act
        var resultado = await RestaurarDesdeArchivoAsync(externa);

        // Assert: se restauró desde la copia hecha en el directorio, y esa ruta quedó en la auditoría.
        (await CategoriasDeAsync(_vigente)).Should().ContainSingle().Which.Should().Be("Datos A");
        File.Exists(copia.RutaArchivo).Should().BeTrue("el .bak se copió al directorio de respaldos");
        File.Exists(Path.ChangeExtension(copia.RutaArchivo, ".json")).Should().BeTrue();
        File.Exists(externa).Should().BeTrue("el original no se toca");

        await using var contexto = ContextoDe(CadenaVigente);
        var registro = await contexto.RegistrosRestauracion.AsNoTracking().SingleAsync(r => r.Referencia == resultado.ReferenciaRestauracion);
        registro.RutaArchivo.Should().Be(copia.RutaArchivo);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_MismoNombreConOtroContenidoEnElDirectorio_RestauraDesdeUnaCopiaConSufijo()
    {
        // Arrange: en el directorio hay otro archivo con el mismo nombre; la copia externa es la buena.
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        Directory.CreateDirectory(_externa);
        var externa = Path.Combine(_externa, Path.GetFileName(copia.RutaArchivo));
        File.Copy(copia.RutaArchivo, externa);
        File.Copy(Path.ChangeExtension(copia.RutaArchivo, ".json"), Path.ChangeExtension(externa, ".json"));
        CorromperArchivo(copia.RutaArchivo);

        // Act
        var resultado = await RestaurarDesdeArchivoAsync(externa);

        // Assert
        (await CategoriasDeAsync(_vigente)).Should().ContainSingle().Which.Should().Be("Datos A");
        var conSufijo = Path.Combine(_directorio, $"{Path.GetFileNameWithoutExtension(copia.RutaArchivo)}_2.bak");
        File.Exists(conSufijo).Should().BeTrue();
        File.Exists(Path.ChangeExtension(conSufijo, ".json")).Should().BeTrue();

        await using var contexto = ContextoDe(CadenaVigente);
        var registro = await contexto.RegistrosRestauracion.AsNoTracking().SingleAsync(r => r.Referencia == resultado.ReferenciaRestauracion);
        registro.RutaArchivo.Should().Be(conSufijo);
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_CopiaYaEnElDirectorio_NoLaDuplica()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        var antes = Directory.GetFiles(_directorio, "*.bak").Length;

        // Act
        var resultado = await RestaurarDesdeArchivoAsync(copia.RutaArchivo);

        // Assert: solo se sumó la copia previa a la restauración, ningún duplicado de la elegida.
        Directory.GetFiles(_directorio, "*_2.bak").Should().BeEmpty();
        Directory.GetFiles(_directorio, "*.bak").Length.Should().Be(antes + 1);

        await using var contexto = ContextoDe(CadenaVigente);
        var registro = await contexto.RegistrosRestauracion.AsNoTracking().SingleAsync(r => r.Referencia == resultado.ReferenciaRestauracion);
        registro.RutaArchivo.Should().Be(copia.RutaArchivo);
    }

    // ───────────────────── Fallas: la vigente no se toca ─────────────────────

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_PasswordIncorrectaEnLaCopia_LanzaCredencialesInvalidasYNoToca()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();

        // Act
        var act = () => RestaurarDesdeArchivoAsync(copia.RutaArchivo, "ClaveIncorrecta1!");

        // Assert
        await act.Should().ThrowAsync<CredencialesInvalidasException>();
        (await CategoriasDeAsync(_vigente)).Should().BeEquivalentTo("Datos A", "Datos B");
        (await ExisteBaseAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ListarBasesAnterioresAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_ArchivoModificadoDespuesDeLaCopia_LanzaRestauracionFallidaYNoToca()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        CorromperArchivo(copia.RutaArchivo);

        // Act
        var act = () => RestaurarDesdeArchivoAsync(copia.RutaArchivo);

        // Assert: la huella SHA-256 del manifiesto ya no coincide.
        await act.Should().ThrowAsync<RestauracionFallidaException>();
        (await CategoriasDeAsync(_vigente)).Should().BeEquivalentTo("Datos A", "Datos B");
        (await ExisteBaseAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ListarBasesAnterioresAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_TurnoDeCajaAbierto_LanzaRestauracionFallidaYNoRestauraNada()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        await using (var contexto = ContextoDe(CadenaVigente))
        {
            contexto.TurnosCaja.Add(TurnoCaja.Abrir(_idGerente, 1000m));
            await contexto.SaveChangesAsync();
        }

        // Act
        var act = () => RestaurarDesdeHistorialAsync(copia.Referencia);

        // Assert (R-11)
        var excepcion = await act.Should().ThrowAsync<RestauracionFallidaException>();
        excepcion.Which.Message.Should().Contain("turno");
        (await CategoriasDeAsync(_vigente)).Should().BeEquivalentTo("Datos A", "Datos B");
        (await ExisteBaseAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ListarBasesAnterioresAsync()).Should().BeEmpty();
    }

    // ───────────────────── Restauraciones repetidas ─────────────────────

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_DosVecesSeguidas_ConservaSoloLaBaseAnteriorMasReciente()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();

        // Act: el nombre de la base anterior tiene resolución de segundos, por eso se espera entre ambas.
        var primera = await RestaurarDesdeArchivoAsync(copia.RutaArchivo);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        var segunda = await RestaurarDesdeArchivoAsync(copia.RutaArchivo);

        // Assert (D-20)
        primera.BaseAnterior.Should().NotBe(segunda.BaseAnterior);
        (await ListarBasesAnterioresAsync()).Should().ContainSingle().Which.Should().Be(segunda.BaseAnterior);
        (await ExisteBaseAsync(primera.BaseAnterior!)).Should().BeFalse();
        (await CategoriasDeAsync(_vigente)).Should().ContainSingle().Which.Should().Be("Datos A");

        await using var contexto = ContextoDe(CadenaVigente);
        (await contexto.RegistrosRestauracion.AsNoTracking().CountAsync()).Should().Be(2, "el registro sobrevive a nuevos restores");
    }

    // ───────────────────── Bloqueo ─────────────────────

    [Fact]
    public async Task CrearBackupManualAsync_RestauracionEnCurso_LanzaBackupFallidoYFuncionaAlLiberar()
    {
        // Arrange
        await PrepararBaseConGerenteAsync();
        await using var scopeRestauracion = _servicios.CreateAsyncScope();
        var bloqueo = scopeRestauracion.ServiceProvider.GetRequiredService<IBloqueoRestauracion>();
        var exclusividad = await bloqueo.AdquirirExclusivoAsync();

        // Act
        Func<Task> conBloqueo = () => CrearBackupManualAsync();
        try
        {
            await conBloqueo.Should().ThrowAsync<BackupFallidoException>();
        }
        finally
        {
            await exclusividad.DisposeAsync();
        }

        var resultado = await CrearBackupManualAsync();

        // Assert
        File.Exists(resultado.RutaArchivo).Should().BeTrue();
        resultado.RegistradaEnHistorial.Should().BeTrue();
    }

    // ───────────────────── PC nueva ─────────────────────

    [Fact]
    public async Task RestaurarDesdeArchivoAsync_SinBaseVigente_RestauraSinCopiaPreviaYAdvierte()
    {
        // Arrange
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        await EliminarVigenteAsync();
        (await ExisteBaseAsync(_vigente)).Should().BeFalse();

        // Act
        var resultado = await RestaurarDesdeArchivoAsync(copia.RutaArchivo);

        // Assert (D-26)
        resultado.ReferenciaCopiaPrevia.Should().BeNull();
        resultado.BaseAnterior.Should().BeNull();
        resultado.Advertencias.Should().Contain(a => a.Contains("No existe una base vigente", StringComparison.Ordinal));
        (await ExisteBaseAsync(_vigente)).Should().BeTrue();
        (await ExisteBaseAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ListarBasesAnterioresAsync()).Should().BeEmpty();
        (await CategoriasDeAsync(_vigente)).Should().ContainSingle().Which.Should().Be("Datos A");

        await using var contexto = ContextoDe(CadenaVigente);
        var registro = await contexto.RegistrosRestauracion.AsNoTracking().SingleAsync();
        registro.Origen.Should().Be(OrigenRestauracionEnum.Consola);
        registro.ReferenciaCopiaPrevia.Should().BeNull();
        registro.BaseAnterior.Should().BeNull();
    }

    // ───────────────────── Copias huérfanas (B-0) ─────────────────────

    /// <summary>
    /// Deja en la vigente una copia <c>EnCurso</c> cuyo proceso "murió": nadie sostiene el bloqueo compartido.
    /// </summary>
    private async Task<Guid> InsertarCopiaHuerfanaEnLaVigenteAsync(bool murioVerificando = false)
    {
        await using var contexto = ContextoDe(CadenaVigente);
        var ultimaMigracion = contexto.Database.GetMigrations().Last();
        var huerfana = RegistroBackup.Iniciar(
            TipoBackupEnum.Automatico, null, null, Path.Combine(_directorio, "huerfana.bak"), ultimaMigracion, "1.0.0");
        if (murioVerificando)
        {
            // El motor terminó el BACKUP y el proceso murió antes de marcar la copia como verificada.
            huerfana.RegistrarExito(4096, new string('a', 64));
        }

        contexto.RegistrosBackup.Add(huerfana);
        await contexto.SaveChangesAsync();
        return huerfana.Referencia;
    }

    [Fact]
    public async Task CrearBackupManualAsync_OtraCopiaViva_NoCierraNiTocaSuRegistro()
    {
        // Arrange: una copia legítima en vuelo (EnCurso y con el bloqueo compartido tomado), detenida a mitad del BACKUP.
        await PrepararBaseConGerenteAsync();
        _compuerta.Armada = true;
        var primera = Task.Run(CrearBackupManualAsync);
        await _compuerta.Entro.WaitAsync(TimeSpan.FromSeconds(30));

        // Act: otra copia arranca: su B-0 no puede tomar el exclusivo, así que no debe tocar nada.
        var segunda = () => CrearBackupManualAsync();

        // Assert
        await segunda.Should().ThrowAsync<DomainException>().WithMessage("*en curso*");
        await using (var contexto = ContextoDe(CadenaVigente))
        {
            var enCurso = await contexto.RegistrosBackup.AsNoTracking().SingleAsync();
            enCurso.Estado.Should().Be(EstadoBackupEnum.EnCurso, "la copia viva sigue su camino");
            enCurso.MensajeError.Should().BeNull();
        }

        _compuerta.Liberar();
        var resultado = await primera;
        await using var final = ContextoDe(CadenaVigente);
        (await final.RegistrosBackup.AsNoTracking().SingleAsync(r => r.Referencia == resultado.Referencia))
            .Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupManualAsync_ConUnaCopiaExitosaSinVerificarYSusRestos_LaCierraComoFallidaYBorraLosArchivos()
    {
        // Arrange: el proceso murió durante la verificación; quedaron el .bak y el manifiesto parciales en el directorio.
        await PrepararBaseConGerenteAsync();
        var referenciaHuerfana = await InsertarCopiaHuerfanaEnLaVigenteAsync(murioVerificando: true);
        var bakHuerfano = Path.Combine(_directorio, "huerfana.bak");
        var jsonHuerfano = Path.Combine(_directorio, "huerfana.json");
        await File.WriteAllBytesAsync(bakHuerfano, [1, 2, 3]);
        await File.WriteAllTextAsync(jsonHuerfano, "{}");

        // Act
        var resultado = await CrearBackupManualAsync();

        // Assert: Exitoso quedaba para siempre ocupando un lugar de la retención; ahora Fallido y sin archivos.
        await using var contexto = ContextoDe(CadenaVigente);
        var registros = await contexto.RegistrosBackup.AsNoTracking().ToListAsync();
        var huerfana = registros.Single(r => r.Referencia == referenciaHuerfana);
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        File.Exists(bakHuerfano).Should().BeFalse();
        File.Exists(jsonHuerfano).Should().BeFalse();
        registros.Single(r => r.Referencia == resultado.Referencia).Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupManualAsync_ConUnaCopiaHuerfanaEnLaVigente_LaCierraComoFallidaYCopia()
    {
        // Arrange
        await PrepararBaseConGerenteAsync();
        var referenciaHuerfana = await InsertarCopiaHuerfanaEnLaVigenteAsync();

        // Act
        var resultado = await CrearBackupManualAsync();

        // Assert
        resultado.RegistradaEnHistorial.Should().BeTrue();
        await using var contexto = ContextoDe(CadenaVigente);
        var registros = await contexto.RegistrosBackup.AsNoTracking().ToListAsync();
        var huerfana = registros.Single(r => r.Referencia == referenciaHuerfana);
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        registros.Single(r => r.Referencia == resultado.Referencia).Estado.Should().Be(EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task RestaurarDesdeHistorialAsync_ConUnaCopiaHuerfanaEnLaVigente_LaCierraYElHistorialRestauradoLaConserva()
    {
        // Arrange: sin B-0, la copia huérfana rechazaría la restauración incluso en una recuperación ante desastres.
        var copia = await PrepararCopiaConDatosAYCambiarABAsync();
        var referenciaHuerfana = await InsertarCopiaHuerfanaEnLaVigenteAsync();

        // Act
        var resultado = await RestaurarDesdeHistorialAsync(copia.Referencia);

        // Assert
        resultado.Advertencias.Should().Contain("Se cerró 1 copia de seguridad interrumpida en la base vigente.");
        await using var contexto = ContextoDe(CadenaVigente);
        var registros = await contexto.RegistrosBackup.AsNoTracking().ToListAsync();
        var huerfana = registros.Single(r => r.Referencia == referenciaHuerfana);
        huerfana.Estado.Should().Be(EstadoBackupEnum.Fallido);
        huerfana.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        registros.Should().NotContain(r => r.Estado == EstadoBackupEnum.EnCurso);
    }
}
