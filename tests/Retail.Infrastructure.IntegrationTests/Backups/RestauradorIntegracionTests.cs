using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Retail.Application.Exceptions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Restauración real contra SQL Server Express (RF-22, D-17): restore a una base temporal, descarte ante fallas, espacio en disco,
/// intercambio con la vigente e importación del historial. Cada prueba usa su propia base vigente y limpia todas las
/// bases derivadas (<c>_Restaurando</c>, <c>_Anterior_*</c>) aunque falle.
/// </summary>
public class RestauradorIntegracionTests : IAsyncLifetime
{
    private readonly string _vigente = $"RetailDb_Test_Restaurador_{Guid.NewGuid():N}";
    private readonly string _directorio = ServidorDePruebas.CrearCarpetaAccesibleParaElMotor("RetailRestauradorTests");
    private RetailDbContext _context = null!;

    private static string CadenaDe(string baseDeDatos) => ServidorDePruebas.CadenaDeConexion(baseDeDatos);

    private static RetailDbContext ContextoDe(string cadena)
    {
        return new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(cadena).Options);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directorio);
        _context = ContextoDe(CadenaDe(_vigente));
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        SqlConnection.ClearAllPools();

        // Todas las bases que esta clase pudo crear llevan el nombre de la vigente como prefijo (el GUID es único).
        var nombres = new List<string>();
        await using (var master = new SqlConnection(CadenaDe("master")))
        {
            await master.OpenAsync();
            await using var consulta = master.CreateCommand();
            consulta.CommandText = "SELECT name FROM sys.databases WHERE name LIKE @patron;";
            consulta.Parameters.AddWithValue("@patron", _vigente + "%");
            await using var lector = await consulta.ExecuteReaderAsync();
            while (await lector.ReadAsync())
            {
                nombres.Add(lector.GetString(0));
            }

            foreach (var nombre in nombres)
            {
                await using var comando = master.CreateCommand();
                var delimitado = $"[{nombre.Replace("]", "]]", StringComparison.Ordinal)}]";
                comando.CommandText = $"BEGIN TRY ALTER DATABASE {delimitado} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; END TRY BEGIN CATCH END CATCH; " +
                    $"DROP DATABASE {delimitado};";
                await comando.ExecuteNonQueryAsync();
            }
        }

        SqlConnection.ClearAllPools();

        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    private SqlServerRestauradorBaseDatos CrearSut(long espacioLibreMinimoMb = 1)
    {
        var opciones = Options.Create(new BackupOptions { Directorio = _directorio, EspacioLibreMinimoMb = espacioLibreMinimoMb });
        return new SqlServerRestauradorBaseDatos(_context, opciones, NullLoggerFactory.Instance);
    }

    private async Task<string> CrearCopiaAsync(string nombre = "copia.bak")
    {
        var ruta = Path.Combine(_directorio, nombre);
        await new SqlServerEjecutorBackup(_context).CrearCopiaAsync(ruta, progreso: null);
        return ruta;
    }

    private static Usuario NuevoUsuario(string nombreUsuario, RolUsuarioEnum rol = RolUsuarioEnum.Gerente)
    {
        return Usuario.Crear(nombreUsuario, "Ana", "Perez", "hash-de-prueba", rol, "ana@example.com");
    }

    private static async Task<bool> ExisteBaseAsync(string cadenaMaster, string nombre)
    {
        await using var conexion = new SqlConnection(cadenaMaster);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT DB_ID(@nombre);";
        comando.Parameters.AddWithValue("@nombre", nombre);
        var resultado = await comando.ExecuteScalarAsync();
        return resultado is not null and not DBNull;
    }

    private static Task<bool> ExisteAsync(string nombre) => ExisteBaseAsync(CadenaDe("master"), nombre);

    private static async Task<List<string>> ArchivosFisicosAsync(string baseDeDatos)
    {
        var archivos = new List<string>();
        await using var conexion = new SqlConnection(CadenaDe("master"));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(@nombre);";
        comando.Parameters.AddWithValue("@nombre", baseDeDatos);
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            archivos.Add(lector.GetString(0));
        }

        return archivos;
    }

    private static async Task<List<string>> NombresDeUsuariosAsync(string baseDeDatos)
    {
        await using var contexto = ContextoDe(CadenaDe(baseDeDatos));
        return await contexto.Usuarios.Select(u => u.NombreUsuario).ToListAsync();
    }

    private async Task AgregarYGuardarAsync(params Usuario[] usuarios)
    {
        _context.Usuarios.AddRange(usuarios);
        await _context.SaveChangesAsync();
    }

    private sealed class ProgresoSincronico : IProgress<int>
    {
        public List<int> Valores { get; } = [];

        public void Report(int value) => Valores.Add(value);
    }

    // ───────────────────── Existencia ─────────────────────

    [Fact]
    public async Task ExisteBaseVigenteAsync_BaseCreada_DevuelveTrue()
    {
        (await CrearSut().ExisteBaseVigenteAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ExisteBaseVigenteAsync_BaseInexistente_DevuelveFalse()
    {
        await using var otro = ContextoDe(CadenaDe($"RetailDb_Test_Restaurador_Inexistente_{Guid.NewGuid():N}"));
        var sut = new SqlServerRestauradorBaseDatos(otro, Options.Create(new BackupOptions()), NullLoggerFactory.Instance);

        (await sut.ExisteBaseVigenteAsync()).Should().BeFalse();
    }

    // ───────────────────── Restore a la base temporal ─────────────────────

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_CopiaValida_CreaLaTemporalConLosDatosYArchivosPropios()
    {
        // Arrange
        await AgregarYGuardarAsync(NuevoUsuario("gerente.uno"));
        var ruta = await CrearCopiaAsync();
        var progreso = new ProgresoSincronico();

        // Act
        await using var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso);

        // Assert
        var temporal = $"{_vigente}_Restaurando";
        (await ExisteAsync(temporal)).Should().BeTrue();
        (await NombresDeUsuariosAsync(temporal)).Should().Contain("gerente.uno");
        progreso.Valores.Should().NotBeEmpty().And.OnlyContain(p => p >= 0 && p <= 100);

        var archivosTemporal = await ArchivosFisicosAsync(temporal);
        var archivosVigente = await ArchivosFisicosAsync(_vigente);
        archivosTemporal.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
        archivosTemporal.Intersect(archivosVigente, StringComparer.OrdinalIgnoreCase).Should().BeEmpty();
        archivosTemporal.Should().OnlyContain(a => Path.GetFileName(a).StartsWith(_vigente, StringComparison.Ordinal));

        restaurada.NombreBaseAnterior.Should().StartWith($"{_vigente}_Anterior_");
    }

    /// <summary>
    /// Crea en la base indicada el usuario de la copia automática SIN login: así la prueba no toca el login real
    /// <c>NT AUTHORITY\SYSTEM</c> de la instancia y el usuario igual figura en el catálogo de la base.
    /// </summary>
    private static async Task CrearUsuarioDeLaTareaProgramadaAsync(string baseDeDatos)
    {
        await using var conexion = new SqlConnection(CadenaDe(baseDeDatos));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "CREATE USER [NT AUTHORITY\\SYSTEM] WITHOUT LOGIN;";
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task VigenteTieneUsuarioDeLaTareaProgramadaAsync_SegunLaBase_ReflejaSiElUsuarioExiste()
    {
        // Arrange
        var sut = CrearSut();
        var antes = await sut.VigenteTieneUsuarioDeLaTareaProgramadaAsync();

        // Act
        await CrearUsuarioDeLaTareaProgramadaAsync(_vigente);
        var despues = await sut.VigenteTieneUsuarioDeLaTareaProgramadaAsync();

        // Assert
        antes.Should().BeFalse();
        despues.Should().BeTrue();
    }

    [Fact]
    public async Task TieneUsuarioDeLaTareaProgramadaAsync_CopiaTomadaConYSinElUsuario_DistingueUnaDeOtra()
    {
        // Arrange: la copia "vieja" se toma antes de registrar la tarea programada; la "nueva", después.
        var rutaSinUsuario = await CrearCopiaAsync("sin-usuario.bak");
        await CrearUsuarioDeLaTareaProgramadaAsync(_vigente);
        var rutaConUsuario = await CrearCopiaAsync("con-usuario.bak");

        // Act
        bool sinUsuario;
        await using (var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(rutaSinUsuario, progreso: null))
        {
            sinUsuario = await restaurada.TieneUsuarioDeLaTareaProgramadaAsync();
        }

        bool conUsuario;
        await using (var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(rutaConUsuario, progreso: null))
        {
            conUsuario = await restaurada.TieneUsuarioDeLaTareaProgramadaAsync();
        }

        // Assert
        sinUsuario.Should().BeFalse();
        conUsuario.Should().BeTrue();
    }

    [Fact]
    public async Task VigenteTieneUsuarioDeLaTareaProgramadaAsync_BaseInexistente_DevuelveFalso()
    {
        // Arrange: PC nueva, la vigente todavía no existe.
        await _context.DisposeAsync();
        SqlConnection.ClearAllPools();
        await using (var master = new SqlConnection(CadenaDe("master")))
        {
            await master.OpenAsync();
            await using var comando = master.CreateCommand();
            comando.CommandText = $"ALTER DATABASE [{_vigente}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_vigente}];";
            await comando.ExecuteNonQueryAsync();
        }

        _context = ContextoDe(CadenaDe(_vigente));

        // Act
        var resultado = await CrearSut().VigenteTieneUsuarioDeLaTareaProgramadaAsync();

        // Assert
        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_RutaDeRed_LanzaRestauracionFallida()
    {
        var act = () => CrearSut().RestaurarEnBaseTemporalAsync(@"\\servidor\recurso\copia.bak", progreso: null);

        await act.Should().ThrowAsync<RestauracionFallidaException>();
    }

    [Fact]
    public async Task DisposeAsync_SinPromover_EliminaLaBaseTemporal()
    {
        var ruta = await CrearCopiaAsync();
        var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);
        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeTrue();

        await restaurada.DisposeAsync();
        await restaurada.DisposeAsync();

        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ExisteAsync(_vigente)).Should().BeTrue();
    }

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_TemporalHuerfana_LaReemplaza()
    {
        // Arrange: un corte anterior dejó la temporal con otro contenido.
        await AgregarYGuardarAsync(NuevoUsuario("gerente.dos"));
        var ruta = await CrearCopiaAsync();
        await using (var primera = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            // Se simula el corte: la temporal queda sin liberar al abandonar el objeto sin DisposeAsync de la base.
            await using var contexto = ContextoDe(CadenaDe($"{_vigente}_Restaurando"));
            contexto.Usuarios.Add(NuevoUsuario("huerfano"));
            await contexto.SaveChangesAsync();
            SqlConnection.ClearAllPools();

            // Act: una segunda restauración sobre la misma base temporal.
            await using var segunda = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);

            // Assert
            var usuarios = await NombresDeUsuariosAsync($"{_vigente}_Restaurando");
            usuarios.Should().Contain("gerente.dos").And.NotContain("huerfano");
        }
    }

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_ArchivoCorrupto_LanzaRestauracionFallidaYNoDejaTemporal()
    {
        // Arrange: se dañan bytes en medio del archivo.
        var ruta = await CrearCopiaAsync("sana.bak");
        var corrupta = Path.Combine(_directorio, "corrupta.bak");
        File.Copy(ruta, corrupta);
        await using (var archivo = new FileStream(corrupta, FileMode.Open, FileAccess.Write))
        {
            archivo.Seek(archivo.Length / 2, SeekOrigin.Begin);
            await archivo.WriteAsync(Enumerable.Repeat((byte)0xFF, 8192).ToArray());
        }

        // Act
        var act = () => CrearSut().RestaurarEnBaseTemporalAsync(corrupta, progreso: null);

        // Assert
        var excepcion = await act.Should().ThrowAsync<RestauracionFallidaException>();
        excepcion.Which.InnerException.Should().BeOfType<SqlException>();
        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ExisteAsync(_vigente)).Should().BeTrue();
    }

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_ArchivoInexistente_LanzaRestauracionFallida()
    {
        var act = () => CrearSut().RestaurarEnBaseTemporalAsync(Path.Combine(_directorio, "noexiste.bak"), progreso: null);

        await act.Should().ThrowAsync<RestauracionFallidaException>();
        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeFalse();
    }

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_EspacioInsuficiente_LanzaAntesDeRestaurar()
    {
        // Arrange: un margen imposible de cumplir (≈ 2^62 bytes).
        var ruta = await CrearCopiaAsync();

        // Act
        var act = () => CrearSut(espacioLibreMinimoMb: long.MaxValue / (1L << 21)).RestaurarEnBaseTemporalAsync(ruta, progreso: null);

        // Assert
        await act.Should().ThrowAsync<RestauracionFallidaException>().WithMessage("*espacio*");
        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeFalse();
    }

    // ───────────────────── Intercambio ─────────────────────

    [Fact]
    public async Task PromoverAVigenteAsync_ConDatosPosterioresALaCopia_LaVigenteRecuperaLaCopiaYLaAnteriorConservaLoDemas()
    {
        // Arrange
        await AgregarYGuardarAsync(NuevoUsuario("gerente.tres"));
        var ruta = await CrearCopiaAsync();
        await AgregarYGuardarAsync(NuevoUsuario("posterior", RolUsuarioEnum.Cajero));

        // Act
        string? anterior;
        await using (var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            anterior = restaurada.NombreBaseAnterior;
            await restaurada.PromoverAVigenteAsync();
        }

        // Assert
        anterior.Should().NotBeNull();
        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await NombresDeUsuariosAsync(_vigente)).Should().Contain("gerente.tres").And.NotContain("posterior");
        (await NombresDeUsuariosAsync(anterior!)).Should().Contain("posterior");
    }

    [Fact]
    public async Task PromoverAVigenteAsync_FallaElSegundoRenombrado_RevierteElPrimeroYLaVigenteQuedaIntacta()
    {
        // Arrange: la temporal se pone OFFLINE justo antes del intercambio. El paso 1 (apartar la vigente) funciona y el
        // paso 2 (renombrar la temporal) falla aun con reintentos: es la rama que evita quedarse sin base (M-2).
        await AgregarYGuardarAsync(NuevoUsuario("gerente.reversion"));
        var ruta = await CrearCopiaAsync();
        await AgregarYGuardarAsync(NuevoUsuario("posterior.reversion", RolUsuarioEnum.Cajero));
        var temporal = $"{_vigente}_Restaurando";

        // Act
        string? anterior;
        Func<Task> act;
        await using (var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            anterior = restaurada.NombreBaseAnterior;
            await EjecutarEnMasterAsync($"ALTER DATABASE [{temporal}] SET OFFLINE WITH ROLLBACK IMMEDIATE;");
            act = () => restaurada.PromoverAVigenteAsync();

            // Assert: falla con un mensaje que dice que la vigente no cambió.
            await act.Should().ThrowAsync<Retail.Application.Exceptions.RestauracionFallidaException>()
                .WithMessage("*se conservó sin cambios*");

            // La temporal vuelve a estar en línea para que el descarte pueda borrar también sus archivos físicos.
            await EjecutarEnMasterAsync($"ALTER DATABASE [{temporal}] SET ONLINE;");
        }

        // Assert: la vigente recuperó su nombre, su acceso multiusuario y todos sus datos; no quedó una "_Anterior_".
        anterior.Should().NotBeNull();
        (await ExisteAsync(anterior!)).Should().BeFalse();
        (await AccesoDeUsuariosAsync(_vigente)).Should().Be("MULTI_USER");
        (await NombresDeUsuariosAsync(_vigente)).Should().Contain("gerente.reversion").And.Contain("posterior.reversion");
        (await ExisteAsync(temporal)).Should().BeFalse();
    }

    private static async Task EjecutarEnMasterAsync(string sentencia)
    {
        await using var master = new SqlConnection(CadenaDe("master"));
        await master.OpenAsync();
        await using var comando = master.CreateCommand();
        comando.CommandText = sentencia;
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string> AccesoDeUsuariosAsync(string baseDeDatos)
    {
        await using var master = new SqlConnection(CadenaDe("master"));
        await master.OpenAsync();
        await using var comando = master.CreateCommand();
        comando.CommandText = "SELECT user_access_desc FROM sys.databases WHERE name = @nombre;";
        comando.Parameters.AddWithValue("@nombre", baseDeDatos);
        return (string)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task PromoverAVigenteAsync_DosCiclosSeguidos_FuncionanYSoloSeConservaLaUltimaAnterior()
    {
        // Arrange
        await AgregarYGuardarAsync(NuevoUsuario("gerente.cuatro"));
        var ruta = await CrearCopiaAsync();

        // Act: dos restauraciones consecutivas (los nombres de archivo físicos no deben chocar).
        string primeraAnterior;
        await using (var primera = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            primeraAnterior = primera.NombreBaseAnterior!;
            await primera.PromoverAVigenteAsync();
        }

        // La marca de tiempo tiene resolución de segundos: otra restauración real nunca ocurre en el mismo segundo.
        await Task.Delay(1100);

        string segundaAnterior;
        var sut = CrearSut();
        await using (var segunda = await sut.RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            segundaAnterior = segunda.NombreBaseAnterior!;
            await segunda.PromoverAVigenteAsync();
        }

        var noEliminadas = await sut.EliminarBasesAnterioresAsync(segundaAnterior);

        // Assert
        segundaAnterior.Should().NotBe(primeraAnterior);
        noEliminadas.Should().BeEmpty();
        (await ExisteAsync(primeraAnterior)).Should().BeFalse();
        (await ExisteAsync(segundaAnterior)).Should().BeTrue();
        (await NombresDeUsuariosAsync(_vigente)).Should().Contain("gerente.cuatro");
    }

    [Fact]
    public async Task PromoverAVigenteAsync_SinBaseVigente_CreaLaVigenteSinAnterior()
    {
        // Arrange: la copia se toma y la vigente desaparece (PC nueva).
        await AgregarYGuardarAsync(NuevoUsuario("gerente.cinco"));
        var ruta = await CrearCopiaAsync();
        await _context.Database.EnsureDeletedAsync();
        SqlConnection.ClearAllPools();

        // Act
        await using (var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            restaurada.NombreBaseAnterior.Should().BeNull();
            await restaurada.PromoverAVigenteAsync();
        }

        // Assert
        (await NombresDeUsuariosAsync(_vigente)).Should().Contain("gerente.cinco");
    }

    [Fact]
    public async Task PromoverAVigenteAsync_CasoNormal_AmbasBasesQuedanMultiusuario()
    {
        // Arrange
        var ruta = await CrearCopiaAsync();

        // Act
        string anterior;
        await using (var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null))
        {
            anterior = restaurada.NombreBaseAnterior!;
            await restaurada.PromoverAVigenteAsync();
        }

        // Assert: el intercambio pasa por SINGLE_USER, pero ninguna base debe quedar así: la aplicación no podría usarla.
        (await AccesoDeAsync(_vigente)).Should().Be("MULTI_USER");
        (await AccesoDeAsync(anterior)).Should().Be("MULTI_USER");
    }

    // ───────────────────── Eliminación de bases anteriores ─────────────────────

    [Fact]
    public async Task EliminarBasesAnterioresAsync_SinBaseAConservar_NoEliminaNada()
    {
        // Arrange
        var anterior = $"{_vigente}_Anterior_20200101_000000";
        await CrearBaseVaciaAsync(anterior);

        // Act
        var noEliminadas = await CrearSut().EliminarBasesAnterioresAsync(baseAConservar: null);

        // Assert
        noEliminadas.Should().BeEmpty();
        (await ExisteAsync(anterior)).Should().BeTrue();
    }

    // ───────────────────── Cancelación durante el RESTORE ─────────────────────

    [Fact]
    public async Task RestaurarEnBaseTemporalAsync_CanceladaDuranteElRestore_LanzaCancelacionYNoDejaTemporal()
    {
        // Arrange: se cancela en el primer aviso de avance del motor, con el RESTORE en marcha.
        var ruta = await CrearCopiaAsync();
        using var fuente = new CancellationTokenSource();
        var progreso = new ProgresoQueCancela(fuente);

        // Act
        var act = () => CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso, fuente.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        (await ExisteAsync($"{_vigente}_Restaurando")).Should().BeFalse();
        (await ExisteAsync(_vigente)).Should().BeTrue();
    }

    private sealed class ProgresoQueCancela : IProgress<int>
    {
        private readonly CancellationTokenSource _fuente;

        public ProgresoQueCancela(CancellationTokenSource fuente)
        {
            _fuente = fuente;
        }

        public void Report(int value) => _fuente.Cancel();
    }

    // ───────────────────── Turno de caja abierto en la vigente (D-26) ─────────────────────

    [Fact]
    public async Task HayTurnoDeCajaAbiertoEnBaseVigenteAsync_BaseSinLaTabla_DevuelveFalse()
    {
        // Arrange: una base recién creada, sin ninguna migración.
        var nombre = $"{_vigente}_SinTabla";
        await CrearBaseVaciaAsync(nombre);
        await using var contexto = ContextoDe(CadenaDe(nombre));
        var sut = new SqlServerRestauradorBaseDatos(contexto, Options.Create(new BackupOptions()), NullLoggerFactory.Instance);

        // Act + Assert
        (await sut.HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HayTurnoDeCajaAbiertoEnBaseVigenteAsync_BaseInexistente_DevuelveFalse()
    {
        await using var otro = ContextoDe(CadenaDe($"{_vigente}_Inexistente"));
        var sut = new SqlServerRestauradorBaseDatos(otro, Options.Create(new BackupOptions()), NullLoggerFactory.Instance);

        (await sut.HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HayTurnoDeCajaAbiertoEnBaseVigenteAsync_SinTurnoAbierto_DevuelveFalse()
    {
        (await CrearSut().HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HayTurnoDeCajaAbiertoEnBaseVigenteAsync_ConTurnoAbiertoYUnoCerrado_DevuelveTrue()
    {
        // Arrange
        await InsertarTurnoAsync(_vigente, "Cerrado");
        (await CrearSut().HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeFalse();
        await InsertarTurnoAsync(_vigente, "Abierto");

        // Act + Assert
        (await CrearSut().HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task HayTurnoDeCajaAbiertoEnBaseVigenteAsync_TurnoAbiertoDadoDeBaja_DevuelveFalse()
    {
        await InsertarTurnoAsync(_vigente, "Abierto", dadoDeBaja: true);

        (await CrearSut().HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HayTurnoDeCajaAbiertoEnBaseVigenteAsync_EsquemaDeLaMigracionInicial_DevuelveTrue()
    {
        // Arrange: una vigente vieja, migrada solo hasta la migración que crea la tabla de turnos.
        var nombre = $"{_vigente}_Vieja";
        await using var contexto = ContextoDe(CadenaDe(nombre));
        await contexto.GetService<IMigrator>().MigrateAsync("20260909053800_InitialCreate");
        await InsertarTurnoAsync(nombre, "Abierto");
        var sut = new SqlServerRestauradorBaseDatos(contexto, Options.Create(new BackupOptions()), NullLoggerFactory.Instance);

        // Act + Assert
        (await sut.HayTurnoDeCajaAbiertoEnBaseVigenteAsync()).Should().BeTrue();
    }

    // ───────────────────── Ayudas de esta sección ─────────────────────

    private static async Task CrearBaseVaciaAsync(string nombre)
    {
        await using var master = new SqlConnection(CadenaDe("master"));
        await master.OpenAsync();
        await using var comando = master.CreateCommand();
        comando.CommandText = $"CREATE DATABASE [{nombre.Replace("]", "]]", StringComparison.Ordinal)}];";
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<string?> AccesoDeAsync(string baseDeDatos)
    {
        await using var master = new SqlConnection(CadenaDe("master"));
        await master.OpenAsync();
        await using var comando = master.CreateCommand();
        comando.CommandText = "SELECT user_access_desc FROM sys.databases WHERE name = @nombre;";
        comando.Parameters.AddWithValue("@nombre", baseDeDatos);
        return (string?)await comando.ExecuteScalarAsync();
    }

    /// <summary>
    /// Inserta un turno con SQL crudo y solo las columnas que existen desde la migración inicial, así sirve para
    /// cualquier esquema posterior. Se desactivan las restricciones para no depender de usuarios ni de otras reglas.
    /// </summary>
    private static async Task InsertarTurnoAsync(string baseDeDatos, string estado, bool dadoDeBaja = false)
    {
        await using var conexion = new SqlConnection(CadenaDe(baseDeDatos));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            "ALTER TABLE TURNOS_CAJA NOCHECK CONSTRAINT ALL; " +
            "INSERT INTO TURNOS_CAJA (id_usuario, fecha_apertura, saldo_inicial, total_ventas_efectivo, " +
            "total_ingresos_efectivo, total_egresos_efectivo, saldo_teorico_efectivo, total_ventas_electronicas, " +
            "monto_retenido_en_caja, estado, created_at, deleted_at) " +
            "VALUES (1, SYSUTCDATETIME(), 0, 0, 0, 0, 0, 0, 0, @estado, SYSUTCDATETIME(), " +
            "CASE WHEN @baja = 1 THEN SYSUTCDATETIME() ELSE NULL END);";
        comando.Parameters.AddWithValue("@estado", estado);
        comando.Parameters.AddWithValue("@baja", dadoDeBaja);
        await comando.ExecuteNonQueryAsync();
    }

    // ───────────────────── Historial y validaciones sobre la copia ─────────────────────

    [Fact]
    public async Task ImportarHistorialDeLaBaseVigenteAsync_RegistrosEnAmbasBases_PrevalecenLosDeLaVigente()
    {
        // Arrange: en la copia, un registro cerrado y otro en curso.
        var comun = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\copias\a.bak", "M1", "1.0");
        comun.RegistrarFallo("falla original");
        _context.RegistrosBackup.Add(comun);
        await _context.SaveChangesAsync();

        var enCurso = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\copias\b.bak", "M1", "1.0");
        _context.RegistrosBackup.Add(enCurso);
        await _context.SaveChangesAsync();

        var ruta = await CrearCopiaAsync();

        // En la vigente, después de la copia: el registro en curso terminó mal y apareció uno nuevo.
        enCurso.RegistrarFallo("falla posterior");
        var soloVigente = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\copias\c.bak", "M1", "1.0");
        _context.RegistrosBackup.Add(soloVigente);
        await _context.SaveChangesAsync();

        // Act
        await using var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);
        await restaurada.ImportarHistorialDeLaBaseVigenteAsync();

        // Assert
        await using var lectura = ContextoDe(CadenaDe($"{_vigente}_Restaurando"));
        var registros = await lectura.RegistrosBackup.AsNoTracking().ToListAsync();
        registros.Should().HaveCount(3);
        registros.Single(r => r.Referencia == soloVigente.Referencia).Estado.Should().Be(EstadoBackupEnum.EnCurso);
        registros.Single(r => r.Referencia == enCurso.Referencia).MensajeError.Should().Be("falla posterior");
        registros.Single(r => r.Referencia == comun.Referencia).MensajeError.Should().Be("falla original");
    }

    [Fact]
    public async Task ObtenerCopiasEnCursoAsync_SinImportarHistorial_DevuelveLasEnCursoDeLaCopia()
    {
        // Arrange
        var enCurso = RegistroBackup.Iniciar(TipoBackupEnum.Automatico, null, null, @"C:\copias\d.bak", "M1", "1.0");
        _context.RegistrosBackup.Add(enCurso);
        await _context.SaveChangesAsync();
        var ruta = await CrearCopiaAsync();

        // Act
        await using var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);
        var copias = await restaurada.ObtenerCopiasEnCursoAsync();

        // Assert: con seguimiento, para poder cerrarla con los métodos del agregado y guardar.
        copias.Should().ContainSingle().Which.Referencia.Should().Be(enCurso.Referencia);
        copias[0].RegistrarFallo("interrumpida por la restauración");
        await restaurada.GuardarCambiosAsync();
        (await restaurada.ObtenerCopiasEnCursoAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task BuscarUsuarioActivoAsync_UsuarioActivoYDadoDeBaja_SoloDevuelveElActivo()
    {
        // Arrange
        var baja = NuevoUsuario("gerente.baja");
        baja.MarkAsDeleted();
        await AgregarYGuardarAsync(NuevoUsuario("gerente.activo"), baja);
        var ruta = await CrearCopiaAsync();

        // Act
        await using var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);

        // Assert
        (await restaurada.BuscarUsuarioActivoAsync("gerente.activo")).Should().NotBeNull();
        (await restaurada.BuscarUsuarioActivoAsync("gerente.baja")).Should().BeNull();
        (await restaurada.ExisteGerenteActivoAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ExisteGerenteActivoAsync_SoloGerenteDadoDeBaja_DevuelveFalse()
    {
        // Arrange: la migración puede sembrar usuarios, así que se da de baja a todo Gerente previo.
        foreach (var existente in await _context.Usuarios.Where(u => u.IdRol == (int)RolUsuarioEnum.Gerente).ToListAsync())
        {
            existente.MarkAsDeleted();
        }

        var baja = NuevoUsuario("gerente.dado.de.baja");
        baja.MarkAsDeleted();
        await AgregarYGuardarAsync(baja);
        var ruta = await CrearCopiaAsync();

        // Act
        await using var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);

        // Assert
        (await restaurada.ExisteGerenteActivoAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerEstadoEsquemaAsync_CopiaAlDia_NoTieneMigracionesPendientes()
    {
        var ruta = await CrearCopiaAsync();

        await using var restaurada = await CrearSut().RestaurarEnBaseTemporalAsync(ruta, progreso: null);
        var estado = await restaurada.ObtenerEstadoEsquemaAsync();

        estado.PuedeConectar.Should().BeTrue();
        estado.EstaAlDia.Should().BeTrue();
    }
}
