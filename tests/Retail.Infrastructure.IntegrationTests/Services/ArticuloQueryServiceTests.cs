using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class ArticuloQueryServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_ArtQ_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private ArticuloQueryService _sut = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new ArticuloQueryService(_context);
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
    public async Task ObtenerArticulosPaginadosAsync_ConPaginacion_RetornaCantidadSolicitadaYTotalesGlobales()
    {
        // Arrange
        var cat1 = new Categoria { NombreCategoria = "Librería" };
        var cat2 = new Categoria { NombreCategoria = "Tecnología" };
        _context.Categorias.AddRange(cat1, cat2);
        await _context.SaveChangesAsync();

        var articulos = new List<Articulo>
        {
            new() { CodigoBarras = "111", Descripcion = "Cuaderno A4", IdCategoria = cat1.Id, CostoReposicion = 100, PorcentajeGanancia = 50, PrecioVenta = 150, StockActual = 2, StockMinimo = 5, EsServicio = false },
            new() { CodigoBarras = "222", Descripcion = "Lapicera Azul", IdCategoria = cat1.Id, CostoReposicion = 50, PorcentajeGanancia = 40, PrecioVenta = 70, StockActual = 20, StockMinimo = 10, EsServicio = false },
            new() { CodigoBarras = "333", Descripcion = "Regla 30cm", IdCategoria = cat1.Id, CostoReposicion = 80, PorcentajeGanancia = 50, PrecioVenta = 120, StockActual = 1, StockMinimo = 3, EsServicio = false },
            new() { CodigoBarras = "444", Descripcion = "Pendrive 32GB", IdCategoria = cat2.Id, CostoReposicion = 500, PorcentajeGanancia = 30, PrecioVenta = 650, StockActual = 4, StockMinimo = 2, EsServicio = false },
            new() { CodigoBarras = null, Descripcion = "Servicio de Fotocopia", IdCategoria = null, CostoReposicion = 10, PorcentajeGanancia = 100, PrecioVenta = 20, StockActual = 0, StockMinimo = 0, EsServicio = true }
        };
        _context.Articulos.AddRange(articulos);
        await _context.SaveChangesAsync();

        var consulta = new ConsultaArticulosDto
        {
            Pagina = 1,
            TamanoPagina = 2
        };

        // Act
        var resultado = await _sut.ObtenerArticulosPaginadosAsync(consulta);

        // Assert
        resultado.Items.Should().HaveCount(2);
        resultado.TotalRegistros.Should().Be(5);
        resultado.TotalArticulos.Should().Be(5);
        resultado.TotalAlertasStock.Should().Be(2); // Cuaderno (2 <= 5) y Regla (1 <= 3). El servicio no tiene stock bajo.
        resultado.PaginaActual.Should().Be(1);
        resultado.TamanoPagina.Should().Be(2);
        resultado.TotalPaginas.Should().Be(3);
    }

    [Fact]
    public async Task ObtenerArticulosPaginadosAsync_ConFiltroBusqueda_FiltraPorCodigoYDescripcion()
    {
        // Arrange
        _context.Articulos.AddRange(
            new Articulo { CodigoBarras = "7791234567890", Descripcion = "Resma A4 500H", CostoReposicion = 3000, PorcentajeGanancia = 40, PrecioVenta = 4200, StockActual = 10, StockMinimo = 5, EsServicio = false },
            new Articulo { CodigoBarras = "7799999999999", Descripcion = "Tijera Escolar", CostoReposicion = 200, PorcentajeGanancia = 50, PrecioVenta = 300, StockActual = 8, StockMinimo = 2, EsServicio = false }
        );
        await _context.SaveChangesAsync();

        // Act - Buscar por código
        var resCodigo = await _sut.ObtenerArticulosPaginadosAsync(new ConsultaArticulosDto { TerminoBusqueda = "12345" });
        // Act - Buscar por descripción
        var resDesc = await _sut.ObtenerArticulosPaginadosAsync(new ConsultaArticulosDto { TerminoBusqueda = "Tijera" });

        // Assert
        resCodigo.Items.Should().ContainSingle(a => a.Descripcion == "Resma A4 500H");
        resDesc.Items.Should().ContainSingle(a => a.Descripcion == "Tijera Escolar");
    }

    [Fact]
    public async Task ObtenerArticulosPaginadosAsync_ConSoloStockCritico_RetornaExclusivamenteAlertas()
    {
        // Arrange
        _context.Articulos.AddRange(
            new Articulo { CodigoBarras = "101", Descripcion = "Articulo Critico", CostoReposicion = 100, PorcentajeGanancia = 50, PrecioVenta = 150, StockActual = 1, StockMinimo = 5, EsServicio = false },
            new Articulo { CodigoBarras = "102", Descripcion = "Articulo Normal", CostoReposicion = 100, PorcentajeGanancia = 50, PrecioVenta = 150, StockActual = 20, StockMinimo = 5, EsServicio = false }
        );
        await _context.SaveChangesAsync();

        // Act
        var res = await _sut.ObtenerArticulosPaginadosAsync(new ConsultaArticulosDto { SoloStockCritico = true });

        // Assert
        res.Items.Should().ContainSingle(a => a.Descripcion == "Articulo Critico");
        res.TotalRegistros.Should().Be(1);
    }
}
