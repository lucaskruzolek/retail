using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application;
using Retail.Application.DTOs.Backups;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Restauración de punta a punta contra LocalDB (RF-22, RF-23, D-17 a D-26): copia real con <c>IBackupService</c>,
/// restore real con <c>IRestauracionService</c> y la inyección de dependencias de producción. Cada operación corre en
/// su propio scope, como lo haría la aplicación, porque tras un restore el contexto viejo apunta a una base reemplazada.
/// </summary>
public class RestauracionIntegracionTests : IAsyncLifetime
{
    private const string Servidor = "Server=(localdb)\\mssqllocaldb;Trusted_Connection=True;MultipleActiveResultSets=true";
    private const string NombreGerente = "gerente";
    private const string ClaveGerente = "Clave2026!";

    private readonly string _vigente = $"RetailDb_Test_Restauracion_{Guid.NewGuid():N}";
    private readonly string _directorio = Path.Combine(Path.GetTempPath(), $"RetailRestauracionTests_{Guid.NewGuid():N}");
    private ServiceProvider _servicios = null!;
    private int _idGerente;

    private static string CadenaDe(string baseDeDatos) => $"{Servidor};Database={baseDeDatos}";

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
        contexto.Categorias.Add(new Categoria { NombreCategoria = nombre });
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
}
