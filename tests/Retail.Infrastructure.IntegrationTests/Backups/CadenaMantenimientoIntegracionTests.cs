using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Retail.Infrastructure.Backups;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Backups;

/// <summary>
/// Conexión de mantenimiento (H19): copias, bloqueo y restauración usan <c>ConnectionStrings:Mantenimiento</c> si existe y
/// la cadena de la aplicación si no.
/// </summary>
public class CadenaMantenimientoIntegracionTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Mantenimiento_{Guid.NewGuid():N}";
    private readonly string _directorio = ServidorDePruebas.CrearCarpetaAccesibleParaElMotor("RetailMantenimientoTests");
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directorio);
        _context = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName)).Options);
        await _context.Database.CrearDesdePlantillaAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
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

    /// <summary>Cadena válida al servidor de pruebas, sin indicar la base, con un nombre de aplicación reconocible.</summary>
    private static string CadenaDeMantenimientoSinBase()
    {
        var constructor = new SqlConnectionStringBuilder(ServidorDePruebas.CadenaDeConexion("master"))
        {
            ApplicationName = "Retail.Mantenimiento.Prueba"
        };
        constructor.Remove("Initial Catalog");
        return constructor.ConnectionString;
    }

    [Fact]
    public void Obtener_SinMantenimiento_DevuelveLaCadenaDelContexto()
    {
        var sut = new CadenaConexionMantenimiento(null);

        sut.Obtener(_context).Should().Be(_context.Database.GetConnectionString());
        sut.EstaConfigurada.Should().BeFalse();
    }

    [Fact]
    public void Obtener_ConMantenimientoSinBase_UsaLaConfiguradaYCompletaLaBaseDelContexto()
    {
        var sut = new CadenaConexionMantenimiento(CadenaDeMantenimientoSinBase());

        var cadena = new SqlConnectionStringBuilder(sut.Obtener(_context));

        cadena.ApplicationName.Should().Be("Retail.Mantenimiento.Prueba");
        cadena.InitialCatalog.Should().Be(_dbName);
        sut.EstaConfigurada.Should().BeTrue();
    }

    [Fact]
    public async Task CrearCopiaAsync_ConMantenimientoConfiguradoAlServidorDePruebas_HaceLaCopia()
    {
        var sut = new SqlServerEjecutorBackup(_context, new CadenaConexionMantenimiento(CadenaDeMantenimientoSinBase()));
        var ruta = Path.Combine(_directorio, "mantenimiento.bak");

        var archivo = await sut.CrearCopiaAsync(ruta, progreso: null);

        archivo.TamanoBytes.Should().BeGreaterThan(0);
        File.Exists(ruta).Should().BeTrue();
    }

    [Fact]
    public async Task CrearCopiaAsync_ConMantenimientoInalcanzable_FallaPorqueUsaEsaCadenaYNoLaDelContexto()
    {
        // Prueba de que la cadena de mantenimiento es la que se usa: si apunta a un servidor que no existe, la copia
        // falla aunque la cadena del contexto sea perfectamente válida.
        var inalcanzable = new SqlConnectionStringBuilder
        {
            DataSource = "servidor-que-no-existe.invalid",
            InitialCatalog = _dbName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 2
        }.ConnectionString;
        var sut = new SqlServerEjecutorBackup(_context, new CadenaConexionMantenimiento(inalcanzable));

        var act = () => sut.CrearCopiaAsync(Path.Combine(_directorio, "no.bak"), progreso: null);

        await act.Should().ThrowAsync<SqlException>();
    }

    [Fact]
    public async Task AdquirirExclusivoAsync_ConMantenimientoInalcanzable_FallaPorqueUsaEsaCadena()
    {
        var inalcanzable = new SqlConnectionStringBuilder
        {
            DataSource = "servidor-que-no-existe.invalid",
            InitialCatalog = _dbName,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 2
        }.ConnectionString;
        var sut = new SqlServerBloqueoRestauracion(_context, mantenimiento: new CadenaConexionMantenimiento(inalcanzable));

        var act = () => sut.AdquirirExclusivoAsync();

        await act.Should().ThrowAsync<SqlException>();
    }

    [Fact]
    public async Task AdquirirExclusivoAsync_SinMantenimiento_UsaLaCadenaDelContexto()
    {
        var sut = new SqlServerBloqueoRestauracion(_context);

        await using var bloqueo = await sut.AdquirirExclusivoAsync();

        bloqueo.Should().NotBeNull();
    }

    [Fact]
    public async Task ExisteBaseVigenteAsync_ConMantenimientoSinBase_ResuelveLaBaseDelContexto()
    {
        var sut = new SqlServerRestauradorBaseDatos(
            _context,
            Microsoft.Extensions.Options.Options.Create(new BackupOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance,
            new CadenaConexionMantenimiento(CadenaDeMantenimientoSinBase()));

        (await sut.ExisteBaseVigenteAsync()).Should().BeTrue();
    }
}
