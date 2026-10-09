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

    [Fact]
    public async Task BuscarParaVentaAsync_MasCoincidenciasQueElLimite_DevuelveLasPrimerasPorDescripcionSinTracking()
    {
        // Arrange
        _context.Articulos.AddRange(
            CrearArticulo("Lápiz HB", codigoBarras: "501"),
            CrearArticulo("Lápiz 2B", codigoBarras: "502"),
            CrearArticulo("Lápiz de color", codigoBarras: "503"),
            CrearArticulo("Goma", codigoBarras: "504"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _sut.BuscarParaVentaAsync("Lápiz", limite: 2);

        // Assert: el límite y el orden se aplican en SQL; el contexto no queda siguiendo entidades
        resultado.Select(a => a.Descripcion).Should().Equal("Lápiz 2B", "Lápiz de color");
        _context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerParaVentaPorCodigoBarrasAsync_CodigoExacto_DevuelveSoloEseArticulo()
    {
        // Arrange
        _context.Articulos.AddRange(
            CrearArticulo("Resma A4", codigoBarras: "7791234567890"),
            CrearArticulo("Resma Oficio", codigoBarras: "77912345678901"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _sut.ObtenerParaVentaPorCodigoBarrasAsync("7791234567890");
        var inexistente = await _sut.ObtenerParaVentaPorCodigoBarrasAsync("000");

        // Assert
        resultado!.Descripcion.Should().Be("Resma A4");
        inexistente.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerDisponibilidadAsync_PresentacionYArticuloBorrado_DevuelveDatosDelOrigenYExcluyeElBorrado()
    {
        // Arrange
        var pack = CrearArticulo("Sobre (pack x100)", stock: 4);
        var borrado = CrearArticulo("Cuaderno discontinuado", stock: 9);
        _context.Articulos.AddRange(pack, borrado);
        await _context.SaveChangesAsync();

        var unidad = CrearArticulo("Sobre (unidad)", stock: 2);
        unidad.DefinirComoPresentacionDe(pack, 100);
        _context.Articulos.Add(unidad);
        borrado.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _sut.ObtenerDisponibilidadAsync([unidad.Id, pack.Id, borrado.Id]);

        // Assert
        resultado.Should().HaveCount(2);
        var disponibilidadUnidad = resultado.Single(d => d.IdArticulo == unidad.Id);
        disponibilidadUnidad.StockActual.Should().Be(2);
        disponibilidadUnidad.DescripcionOrigen.Should().Be("Sobre (pack x100)");
        disponibilidadUnidad.UnidadesPorOrigen.Should().Be(100);
        disponibilidadUnidad.StockOrigen.Should().Be(4);
        resultado.Single(d => d.IdArticulo == pack.Id).StockOrigen.Should().BeNull();
        _context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerDisponibilidadAsync_OrigenDadoDeBaja_DevuelveElOrigenNulo()
    {
        // Arrange: la regla 8 de RF-21 impide esta situación desde la aplicación, pero la consulta no debe
        // ofrecer fraccionar un origen borrado si los datos llegaran a quedar así.
        var pack = CrearArticulo("Sobre (pack x100)", stock: 4);
        _context.Articulos.Add(pack);
        await _context.SaveChangesAsync();

        var unidad = CrearArticulo("Sobre (unidad)", stock: 0);
        unidad.DefinirComoPresentacionDe(pack, 100);
        _context.Articulos.Add(unidad);
        pack.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var resultado = await _sut.ObtenerDisponibilidadAsync([unidad.Id]);

        // Assert
        var disponibilidad = resultado.Should().ContainSingle().Subject;
        disponibilidad.DescripcionOrigen.Should().BeNull();
        disponibilidad.StockOrigen.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerArticulosPaginadosAsync_TerminoSinAcentos_EncuentraLaDescripcionAcentuadaYRespetaLaEnie()
    {
        // Arrange: ARTICULOS.descripcion usa Modern_Spanish_CI_AI (la misma columna que busca el POS)
        _context.Articulos.AddRange(
            new Articulo { Descripcion = "Lápiz Negro HB", CostoReposicion = 100, PorcentajeGanancia = 50, PrecioVenta = 150, StockActual = 10, StockMinimo = 1 },
            new Articulo { Descripcion = "Agenda Año 2027", CostoReposicion = 100, PorcentajeGanancia = 50, PrecioVenta = 150, StockActual = 10, StockMinimo = 1 });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var sinAcentos = await _sut.ObtenerArticulosPaginadosAsync(new ConsultaArticulosDto { TerminoBusqueda = "lapiz negro" });
        var conEnie = await _sut.ObtenerArticulosPaginadosAsync(new ConsultaArticulosDto { TerminoBusqueda = "año" });
        var sinEnie = await _sut.ObtenerArticulosPaginadosAsync(new ConsultaArticulosDto { TerminoBusqueda = "ano" });

        // Assert
        sinAcentos.Items.Should().ContainSingle(a => a.Descripcion == "Lápiz Negro HB");
        conEnie.Items.Should().ContainSingle(a => a.Descripcion == "Agenda Año 2027");
        sinEnie.Items.Should().BeEmpty("en castellano la ñ es una letra distinta de la n");
    }

    private static Articulo CrearArticulo(string descripcion, string? codigoBarras = null, int stock = 10)
    {
        return new Articulo
        {
            CodigoBarras = codigoBarras,
            Descripcion = descripcion,
            CostoReposicion = 100,
            PorcentajeGanancia = 50,
            PrecioVenta = 150,
            StockActual = stock,
            StockMinimo = 1
        };
    }
}
