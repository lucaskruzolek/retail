using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Services;
using Retail.Application.Validators.Articulos;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Casos de uso de presentaciones (RF-21) con InventarioService armado con piezas reales contra LocalDB.
/// Verifican lo que los tests unitarios no pueden: que las consultas se traduzcan a SQL y que los cambios persistan.
/// </summary>
public class InventarioPresentacionesIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_InvPres_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private InventarioService _sut = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new InventarioService(
            new Repository<Articulo>(_context),
            new Repository<Categoria>(_context),
            new Repository<Marca>(_context),
            new UnitOfWork(_context),
            new CrearArticuloValidator(),
            new ActualizarArticuloValidator(),
            new CrearPresentacionValidator(),
            new FraccionarValidator());
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
    public async Task CrearPresentacionYFraccionar_StockSuficiente_PersisteAmbosStocks()
    {
        // Arrange
        var origen = await GuardarOrigenAsync(stock: 5);
        _context.ChangeTracker.Clear();

        var presentacion = await _sut.CrearPresentacionAsync(new CrearPresentacionDto
        {
            IdArticuloOrigen = origen.Id,
            UnidadesPorOrigen = 100,
            Descripcion = "Sobre manila (unidad)",
            PorcentajeGanancia = 50m,
            StockMinimo = 10
        });
        _context.ChangeTracker.Clear();

        // Act
        var unidades = await _sut.FraccionarAsync(new FraccionarDto
        {
            IdArticuloDerivado = presentacion.IdArticulo,
            CantidadOrigen = 2
        });
        _context.ChangeTracker.Clear();

        // Assert
        unidades.Should().Be(200);
        var origenLeido = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == origen.Id);
        var derivadoLeido = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == presentacion.IdArticulo);
        origenLeido.StockActual.Should().Be(3);
        derivadoLeido.StockActual.Should().Be(200);
        derivadoLeido.IdArticuloOrigen.Should().Be(origen.Id);
        derivadoLeido.CostoReposicion.Should().Be(10m);
        derivadoLeido.PrecioVenta.Should().Be(15m);
    }

    [Fact]
    public async Task BajaArticuloAsync_OrigenConPresentacionActiva_SeRechazaHastaDarDeBajaLaPresentacion()
    {
        // Arrange
        var origen = await GuardarOrigenAsync(stock: 5);
        var derivado = await GuardarPresentacionAsync(origen);
        _context.ChangeTracker.Clear();

        // Act + Assert: con la presentación activa, la baja del origen se rechaza
        var bajaPrematura = () => _sut.BajaArticuloAsync(origen.Id);
        await bajaPrematura.Should().ThrowAsync<DomainException>().WithMessage("*Sobre manila (unidad)*");
        _context.ChangeTracker.Clear();

        // Act + Assert: dada de baja la presentación, el filtro global la excluye y el origen se puede dar de baja
        await _sut.BajaArticuloAsync(derivado.Id);
        _context.ChangeTracker.Clear();
        await _sut.BajaArticuloAsync(origen.Id);
        _context.ChangeTracker.Clear();

        var articulos = await _context.Articulos.AsNoTracking().IgnoreQueryFilters().ToListAsync();
        articulos.Should().HaveCount(2).And.OnlyContain(a => a.DeletedAt != null);
    }

    [Fact]
    public async Task ActualizarCostoYPrecioAsync_OrigenConPresentacion_PropagaElCosto()
    {
        // Arrange
        var origen = await GuardarOrigenAsync(stock: 5);
        var derivado = await GuardarPresentacionAsync(origen);
        _context.ChangeTracker.Clear();

        // Act
        await _sut.ActualizarCostoYPrecioAsync(origen.Id, 4500m);
        _context.ChangeTracker.Clear();

        // Assert
        var derivadoLeido = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == derivado.Id);
        derivadoLeido.CostoReposicion.Should().Be(45m);
        derivadoLeido.PrecioVenta.Should().Be(67.5m);
    }

    private async Task<Articulo> GuardarOrigenAsync(int stock)
    {
        var origen = new Articulo
        {
            Descripcion = "Sobre manila (pack x100)",
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m,
            StockActual = stock,
            StockMinimo = 1
        };
        _context.Articulos.Add(origen);
        await _context.SaveChangesAsync();
        return origen;
    }

    private async Task<Articulo> GuardarPresentacionAsync(Articulo origen)
    {
        var derivado = new Articulo { Descripcion = "Sobre manila (unidad)", PorcentajeGanancia = 50m };
        derivado.DefinirComoPresentacionDe(origen, 100);
        _context.Articulos.Add(derivado);
        await _context.SaveChangesAsync();
        return derivado;
    }
}
