using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// La base que recibe cada test desde la plantilla debe ser indistinguible de una recién migrada: si la plantilla
/// quedara atrás de las migraciones del código, todas las pruebas de persistencia correrían contra un esquema viejo.
/// </summary>
public class PlantillaDeBaseMigradaTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Plantilla_Clon_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = new RetailDbContext(new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options);
        await _context.Database.CrearDesdePlantillaAsync();
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
    public async Task CrearDesdePlantillaAsync_BaseNueva_TieneTodasLasMigracionesAplicadas()
    {
        // Act
        var pendientes = await _context.Database.GetPendingMigrationsAsync();
        var aplicadas = await _context.Database.GetAppliedMigrationsAsync();

        // Assert
        pendientes.Should().BeEmpty();
        aplicadas.Should().Equal(_context.Database.GetMigrations());
    }
}
