using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Retail.Application;
using Retail.Application.DTOs.Backups;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Copias reales contra SQL Server Express (RF-22): el motor escribe el archivo, se verifica su huella y su legibilidad, y se
/// recorre <c>migrar</c> de punta a punta con la inyección de dependencias de producción.
/// </summary>
public class BackupIntegracionTests : IAsyncLifetime, IDisposable
{
    private const string MigracionAnteriorAlHistorial = "20261013071257_AgregarRolAdministradorYCuentasDeUsuario";

    private readonly string _dbName = $"RetailDb_Test_Copias_{Guid.NewGuid():N}";
    private readonly string _directorio = ServidorDePruebas.CrearCarpetaAccesibleParaElMotor("RetailBackupTests");
    private string _connectionString = string.Empty;
    private RetailDbContext _context = null!;

    public Task InitializeAsync()
    {
        _connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        _context = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(_connectionString).Options);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();

        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    private string RutaNueva(string nombre)
    {
        Directory.CreateDirectory(_directorio);
        return Path.Combine(_directorio, nombre);
    }

    private sealed class ProgresoSincronico : IProgress<int>
    {
        public List<int> Valores { get; } = [];

        public void Report(int value) => Valores.Add(value);
    }

    // ───────────────────── Motor ─────────────────────

    [Fact]
    public async Task CrearCopiaAsync_BaseReal_EscribeElArchivoConTamanoYHashCorrectosEInformaElAvance()
    {
        // Arrange
        await _context.Database.MigrateAsync();
        var sut = new SqlServerEjecutorBackup(_context);
        var ruta = RutaNueva("real.bak");
        var progreso = new ProgresoSincronico();

        // Act
        var archivo = await sut.CrearCopiaAsync(ruta, progreso);

        // Assert: la huella se recalcula por separado para no confiar en la propia implementación.
        var bytes = await File.ReadAllBytesAsync(ruta);
        archivo.TamanoBytes.Should().Be(bytes.LongLength).And.BeGreaterThan(0);
        archivo.HashSha256.Should().Be(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        progreso.Valores.Should().NotBeEmpty().And.OnlyContain(p => p >= 0 && p <= 100);
    }

    [Fact]
    public async Task VerificarCopiaAsync_ArchivoIntacto_NoLanza()
    {
        await _context.Database.MigrateAsync();
        var sut = new SqlServerEjecutorBackup(_context);
        var ruta = RutaNueva("intacta.bak");
        await sut.CrearCopiaAsync(ruta, progreso: null);

        var act = () => sut.VerificarCopiaAsync(ruta);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task VerificarCopiaAsync_ArchivoCorrupto_LanzaSqlException()
    {
        // Arrange: se dañan bytes en medio del archivo, como haría un disco defectuoso.
        await _context.Database.MigrateAsync();
        var sut = new SqlServerEjecutorBackup(_context);
        var ruta = RutaNueva("corrupta.bak");
        await sut.CrearCopiaAsync(ruta, progreso: null);

        await using (var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Write))
        {
            archivo.Seek(archivo.Length / 2, SeekOrigin.Begin);
            await archivo.WriteAsync(Enumerable.Repeat((byte)0xFF, 8192).ToArray());
        }

        // Act
        var act = () => sut.VerificarCopiaAsync(ruta);

        // Assert: un backup que no se puede leer no debe marcarse como restaurable.
        await act.Should().ThrowAsync<SqlException>();
    }

    // ───────────────────── Esquema ─────────────────────

    [Fact]
    public async Task EsquemaBaseDatos_BaseAnteriorAlHistorial_InformaQueNoTieneTablaYSuUltimaMigracion()
    {
        await _context.GetService<IMigrator>().MigrateAsync(MigracionAnteriorAlHistorial);
        var sut = new EsquemaBaseDatos(_context);

        (await sut.ExisteHistorialDeBackupsAsync()).Should().BeFalse();
        (await sut.ObtenerUltimaMigracionAplicadaAsync()).Should().Be(MigracionAnteriorAlHistorial);
    }

    [Fact]
    public async Task EsquemaBaseDatos_BaseAlDia_TieneTablaDeHistorial()
    {
        await _context.Database.MigrateAsync();
        var sut = new EsquemaBaseDatos(_context);

        (await sut.ExisteHistorialDeBackupsAsync()).Should().BeTrue();
        (await sut.ObtenerUltimaMigracionAplicadaAsync()).Should().Be(_context.Database.GetMigrations().Last());
    }

    // ───────────────────── Punta a punta ─────────────────────

    private ServiceProvider ConstruirServicios(int cantidadMaximaCopias = 14)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ArcaSettings:UseMockArca"] = "true",
                ["BackupSettings:Directorio"] = _directorio,
                ["BackupSettings:CantidadMaximaCopias"] = cantidadMaximaCopias.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["BackupSettings:EspacioLibreMinimoMb"] = "1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddAdministracionSistema();
        services.AddInfrastructureServices(configuracion);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public async Task Migrar_BaseDeUnaVersionAnteriorAlHistorial_CopiaVerificaYDocumentaAntesDeMigrar()
    {
        // Arrange: una base en producción de la versión anterior, con datos, que todavía no tiene REGISTROS_BACKUP.
        await _context.GetService<IMigrator>().MigrateAsync(MigracionAnteriorAlHistorial);
        await using var servicios = ConstruirServicios();
        await using var scope = servicios.CreateAsyncScope();
        var administracion = scope.ServiceProvider.GetRequiredService<IAdministracionSistemaService>();

        // Act
        var resultado = await administracion.MigrarAsync();

        // Assert
        var copia = resultado.CopiaPrevia;
        copia.Should().NotBeNull("D-10: nunca se migra una base con datos sin copia previa");
        copia!.RegistradaEnHistorial.Should().BeFalse();
        File.Exists(copia.RutaArchivo).Should().BeTrue();

        var manifiesto = JsonSerializer.Deserialize<ManifiestoBackupDto>(
            await File.ReadAllTextAsync(ArchivosBackup.RutaManifiesto(copia.RutaArchivo)))!;
        manifiesto.Tipo.Should().Be(nameof(TipoBackupEnum.PreMigracion));
        manifiesto.UltimaMigracion.Should().Be(MigracionAnteriorAlHistorial);
        manifiesto.HashSha256.Should().Be(copia.HashSha256);

        resultado.Aplicadas.Should().NotBeEmpty();
        (await scope.ServiceProvider.GetRequiredService<IEsquemaBaseDatos>().ObtenerEstadoAsync()).EstaAlDia.Should().BeTrue();
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_ConRetencionDeUna_ConservaSoloLaUltimaYRegistraElHistorial()
    {
        // Arrange
        await _context.Database.MigrateAsync();
        await using var servicios = ConstruirServicios(cantidadMaximaCopias: 1);

        // Act: dos copias seguidas, cada una en su propio scope (como dos ejecuciones de Retail.Cli backup).
        ResultadoBackupDto primera;
        ResultadoBackupDto segunda;
        await using (var scope = servicios.CreateAsyncScope())
        {
            primera = await scope.ServiceProvider.GetRequiredService<IBackupService>().CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);
        }

        await using (var scope = servicios.CreateAsyncScope())
        {
            segunda = await scope.ServiceProvider.GetRequiredService<IBackupService>().CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);
        }

        // Assert
        segunda.CopiasPurgadas.Should().Equal(primera.RutaArchivo);
        File.Exists(primera.RutaArchivo).Should().BeFalse();
        File.Exists(ArchivosBackup.RutaManifiesto(primera.RutaArchivo)).Should().BeFalse();
        File.Exists(segunda.RutaArchivo).Should().BeTrue();

        await using var lectura = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(_connectionString).Options);
        var estados = await lectura.RegistrosBackup.OrderBy(r => r.Id).Select(r => r.Estado).ToListAsync();
        estados.Should().Equal(EstadoBackupEnum.ArchivoPurgado, EstadoBackupEnum.Verificado);
    }

    [Fact]
    public async Task CrearBackupDelSistemaAsync_ConUnaCopiaHuerfana_LaCierraComoFallidaYCopia()
    {
        // Arrange: una copia que quedó EnCurso porque su proceso murió; nadie sostiene el bloqueo compartido.
        await _context.Database.MigrateAsync();
        var huerfana = RegistroBackup.Iniciar(
            TipoBackupEnum.Automatico, null, null, RutaNueva("huerfana.bak"), _context.Database.GetMigrations().Last(), "1.0.0");
        _context.RegistrosBackup.Add(huerfana);
        await _context.SaveChangesAsync();
        await using var servicios = ConstruirServicios();

        // Act
        ResultadoBackupDto resultado;
        await using (var scope = servicios.CreateAsyncScope())
        {
            resultado = await scope.ServiceProvider.GetRequiredService<IBackupService>().CrearBackupDelSistemaAsync(TipoBackupEnum.Automatico);
        }

        // Assert: sin B-0 la copia habría sido rechazada ("ya hay una copia en curso") para siempre.
        await using var lectura = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>().UseSqlServer(_connectionString).Options);
        var registros = await lectura.RegistrosBackup.AsNoTracking().ToListAsync();
        var cerrada = registros.Single(r => r.Referencia == huerfana.Referencia);
        cerrada.Estado.Should().Be(EstadoBackupEnum.Fallido);
        cerrada.MensajeError.Should().Be(RegistroBackup.MensajeCopiaHuerfana);
        registros.Single(r => r.Referencia == resultado.Referencia).Estado.Should().Be(EstadoBackupEnum.Verificado);
    }
}
