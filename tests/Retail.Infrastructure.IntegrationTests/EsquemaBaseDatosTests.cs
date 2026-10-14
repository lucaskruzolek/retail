using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Verifica contra LocalDB el diagnóstico de esquema que reemplaza al MigrateAsync del arranque (RF-23).
/// </summary>
public class EsquemaBaseDatosTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Esquema_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private EsquemaBaseDatos _sut = null!;

    public Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        _sut = new EsquemaBaseDatos(_context);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_BaseInexistente_InformaQueNoConectaYQueFaltanTodasLasMigraciones()
    {
        // Act
        var estado = await _sut.ObtenerEstadoAsync();

        // Assert
        estado.PuedeConectar.Should().BeFalse();
        estado.EstaAlDia.Should().BeFalse();
        estado.MigracionesPendientes.Should().Equal(_context.Database.GetMigrations());
    }

    [Fact]
    public async Task AplicarMigracionesPendientesAsync_BaseInexistente_LaCreaYQuedaAlDia()
    {
        // Act
        var aplicadas = await _sut.AplicarMigracionesPendientesAsync();
        var estado = await _sut.ObtenerEstadoAsync();

        // Assert
        aplicadas.Should().Equal(_context.Database.GetMigrations());
        estado.EstaAlDia.Should().BeTrue();
        (await _sut.AplicarMigracionesPendientesAsync()).Should().BeEmpty("una segunda ejecución no tiene nada que aplicar");
    }

    [Fact]
    public async Task ObtenerEstadoAsync_MigracionAplicadaQueLaAppNoConoce_LaInformaComoDesconocida()
    {
        // Arrange: simula una base restaurada desde un respaldo de una versión posterior de la aplicación.
        await _sut.AplicarMigracionesPendientesAsync();
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20991231000000_MigracionDelFuturo', '8.0.11');");

        // Act
        var estado = await _sut.ObtenerEstadoAsync();

        // Assert
        estado.MigracionesDesconocidas.Should().Equal("20991231000000_MigracionDelFuturo");
        estado.MigracionesPendientes.Should().BeEmpty();
        estado.EstaAlDia.Should().BeFalse();
    }
}
