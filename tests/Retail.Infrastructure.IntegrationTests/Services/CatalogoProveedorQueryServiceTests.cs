using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Proveedores;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

public class CatalogoProveedorQueryServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_CatQ_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private CatalogoProveedorQueryService _sut = null!;

    public async Task InitializeAsync()
    {
        var connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        _sut = new CatalogoProveedorQueryService(_context);
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

    /// <summary>
    /// SQL Server admite como máximo 2.100 parámetros por consulta. Este test verifica que buscar más códigos
    /// que ese límite funciona: EF Core 8 envía la lista como un único parámetro JSON (OPENJSON).
    /// Protege ante un cambio de versión de EF Core que modifique esa traducción (handoff, sección 6.2).
    /// </summary>
    [Fact]
    public async Task ObtenerMapaPorCodigosProveedorAsync_ConMasDe2100Codigos_RetornaCoincidenciasSinExcepcion()
    {
        // Arrange: 2.500 ítems persistidos para un proveedor
        const int existentes = 2500;
        var proveedor = Proveedor.Crear("Distribuidora Sur", "30-12345678-1");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        _context.CatalogosProveedores.AddRange(Enumerable.Range(1, existentes).Select(i => new CatalogoProveedor
        {
            IdProveedor = proveedor.Id,
            CodigoProveedor = $"COD-{i:D5}",
            DescripcionProveedor = $"Producto {i}",
            CostoReposicion = 100m
        }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // 3.000 códigos buscados: los 2.500 existentes más 500 que no existen
        var codigosBuscados = Enumerable.Range(1, 3000).Select(i => $"COD-{i:D5}").ToList();

        // Act
        var mapa = await _sut.ObtenerMapaPorCodigosProveedorAsync(proveedor.Id, codigosBuscados);

        // Assert
        mapa.Should().HaveCount(existentes);
        mapa.Should().ContainKey("COD-00001").And.ContainKey($"COD-{existentes:D5}");
        mapa.Should().NotContainKey("COD-02501");
    }

    private async Task<Proveedor> CrearProveedorAsync()
    {
        var proveedor = Proveedor.Crear("Distribuidora Centro", "30-12345678-1");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        return proveedor;
    }

    private static CatalogoProveedor CrearItem(int idProveedor, string codigo, string descripcion, string? codigoBarras = null)
    {
        return new CatalogoProveedor
        {
            IdProveedor = idProveedor,
            CodigoProveedor = codigo,
            DescripcionProveedor = descripcion,
            CodigoBarras = codigoBarras,
            CostoReposicion = 100m
        };
    }

    private static Articulo CrearArticulo(string descripcion, string? codigoBarras = null, int? idCatalogo = null)
    {
        return new Articulo
        {
            Descripcion = descripcion,
            CodigoBarras = codigoBarras,
            IdCatalogoProveedor = idCatalogo,
            CostoReposicion = 100m,
            PorcentajeGanancia = 50,
            PrecioVenta = 150m,
            StockActual = 7,
            StockMinimo = 1
        };
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConItemVinculado_ExponeDatosDelArticulo()
    {
        // Arrange
        var proveedor = await CrearProveedorAsync();
        var vinculado = CrearItem(proveedor.Id, "V1", "Item vinculado");
        var libre = CrearItem(proveedor.Id, "L1", "Item libre");
        _context.CatalogosProveedores.AddRange(vinculado, libre);
        await _context.SaveChangesAsync();
        var articulo = CrearArticulo("Mi articulo", idCatalogo: vinculado.Id);
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var res = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id });

        // Assert
        var v = res.Items.Single(i => i.CodigoProveedor == "V1");
        v.EstaVinculado.Should().BeTrue();
        v.IdArticuloVinculado.Should().Be(articulo.Id);
        v.NombreArticuloTienda.Should().Be("Mi articulo");
        v.PrecioVentaTienda.Should().Be(150m);
        v.StockActualTienda.Should().Be(7);
        v.CoincideConArticuloTienda.Should().BeFalse();
        v.ProveedorRazonSocial.Should().Be("Distribuidora Centro");

        var l = res.Items.Single(i => i.CodigoProveedor == "L1");
        l.EstaVinculado.Should().BeFalse();
        l.IdArticuloVinculado.Should().BeNull();
        l.NombreArticuloTienda.Should().BeNull();
        l.PrecioVentaTienda.Should().BeNull();
        l.StockActualTienda.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConFiltrosDeVinculacion_SeparaIncorporadosDeLosPendientes()
    {
        // Arrange
        var proveedor = await CrearProveedorAsync();
        var vinculado = CrearItem(proveedor.Id, "V1", "Item vinculado");
        var libre = CrearItem(proveedor.Id, "L1", "Item libre");
        _context.CatalogosProveedores.AddRange(vinculado, libre);
        await _context.SaveChangesAsync();
        _context.Articulos.Add(CrearArticulo("Mi articulo", idCatalogo: vinculado.Id));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var sinIncorporar = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { EstadoVinculacion = EstadoVinculacionCatalogoEnum.SinIncorporar });
        var yaEnTienda = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { EstadoVinculacion = EstadoVinculacionCatalogoEnum.YaEnTienda });

        // Assert
        sinIncorporar.Items.Should().ContainSingle(i => i.CodigoProveedor == "L1");
        sinIncorporar.TotalRegistros.Should().Be(1);
        yaEnTienda.Items.Should().ContainSingle(i => i.CodigoProveedor == "V1");
        yaEnTienda.TotalRegistros.Should().Be(1);
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConArticuloDadoDeBaja_NoLoCuentaComoVinculado()
    {
        // Arrange
        var proveedor = await CrearProveedorAsync();
        var item = CrearItem(proveedor.Id, "V1", "Item");
        _context.CatalogosProveedores.Add(item);
        await _context.SaveChangesAsync();
        var articulo = CrearArticulo("Dado de baja", idCatalogo: item.Id);
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        articulo.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var res = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { EstadoVinculacion = EstadoVinculacionCatalogoEnum.SinIncorporar });

        // Assert
        var dto = res.Items.Should().ContainSingle().Subject;
        dto.EstaVinculado.Should().BeFalse();
        dto.IdArticuloVinculado.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConCodigoDeBarrasDeArticuloPropioNoVinculado_MarcaLaCoincidencia()
    {
        // Arrange: la comparación no distingue mayúsculas (intercalación por defecto de la columna)
        var proveedor = await CrearProveedorAsync();
        _context.CatalogosProveedores.AddRange(
            CrearItem(proveedor.Id, "C1", "Coincide", "ABC123"),
            CrearItem(proveedor.Id, "C2", "No coincide", "ZZZ999"),
            CrearItem(proveedor.Id, "C3", "Sin codigo"));
        await _context.SaveChangesAsync();
        _context.Articulos.Add(CrearArticulo("Propio", "abc123"));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var res = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id });

        // Assert
        res.Items.Single(i => i.CodigoProveedor == "C1").CoincideConArticuloTienda.Should().BeTrue();
        res.Items.Single(i => i.CodigoProveedor == "C2").CoincideConArticuloTienda.Should().BeFalse();
        res.Items.Single(i => i.CodigoProveedor == "C3").CoincideConArticuloTienda.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConArticuloYaVinculadoConElMismoCodigoDeBarras_NoMarcaCoincidencia()
    {
        // Arrange: un artículo vinculado a otro ítem no cuenta como "propio no vinculado"
        var proveedor = await CrearProveedorAsync();
        var otro = CrearItem(proveedor.Id, "O1", "Otro");
        var candidato = CrearItem(proveedor.Id, "C1", "Candidato", "555");
        _context.CatalogosProveedores.AddRange(otro, candidato);
        await _context.SaveChangesAsync();
        _context.Articulos.Add(CrearArticulo("Vinculado a otro", "555", otro.Id));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var res = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id });

        // Assert
        res.Items.Single(i => i.CodigoProveedor == "C1").CoincideConArticuloTienda.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConDescripcionesIguales_DesempataPorIdYPaginaSinRepetir()
    {
        // Arrange
        var proveedor = await CrearProveedorAsync();
        var i1 = CrearItem(proveedor.Id, "A1", "Igual");
        var i2 = CrearItem(proveedor.Id, "A2", "Igual");
        var i3 = CrearItem(proveedor.Id, "A3", "Igual");
        _context.CatalogosProveedores.AddRange(i1, i2, i3);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var pag1 = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { Pagina = 1, TamañoPagina = 2 });
        var pag2 = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { Pagina = 2, TamañoPagina = 2 });

        // Assert
        pag1.Items.Select(i => i.Id).Should().Equal(i1.Id, i2.Id);
        pag2.Items.Select(i => i.Id).Should().Equal(i3.Id);
        pag1.TotalRegistros.Should().Be(3);
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_TerminoSinAcentos_EncuentraLaDescripcionAcentuadaYRespetaLaEnie()
    {
        // Arrange: descripcion_proveedor usa Modern_Spanish_CI_AI
        var proveedor = Proveedor.Crear("Distribuidora Norte", "30-87654321-0");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        _context.CatalogosProveedores.AddRange(
            new CatalogoProveedor { IdProveedor = proveedor.Id, CodigoProveedor = "A1", DescripcionProveedor = "Compás escolar metálico", CostoReposicion = 100m },
            new CatalogoProveedor { IdProveedor = proveedor.Id, CodigoProveedor = "A2", DescripcionProveedor = "Agenda año 2027", CostoReposicion = 100m });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var sinAcentos = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id, TerminoBusqueda = "COMPAS ESCOLAR" });
        var conEnie = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id, TerminoBusqueda = "año" });
        var sinEnie = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto { IdProveedor = proveedor.Id, TerminoBusqueda = "ano" });

        // Assert
        sinAcentos.Items.Should().ContainSingle(i => i.CodigoProveedor == "A1");
        conEnie.Items.Should().ContainSingle(i => i.CodigoProveedor == "A2");
        sinEnie.Items.Should().BeEmpty("en castellano la ñ es una letra distinta de la n");
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ConBusqueda_FechaUltimaPlanillaEsLaMasRecienteDelCatalogoVigenteDelProveedor()
    {
        // Arrange
        var proveedor = await CrearProveedorAsync();
        var otroProveedor = Proveedor.Crear("Distribuidora Norte", "30-87654321-0");
        _context.Proveedores.Add(otroProveedor);
        await _context.SaveChangesAsync();

        var antiguo = CrearItem(proveedor.Id, "A-1", "Cuaderno rayado");
        antiguo.FechaActualizacion = new DateTime(2026, 10, 1, 12, 0, 0);
        var reciente = CrearItem(proveedor.Id, "A-2", "Lápiz negro");
        reciente.FechaActualizacion = new DateTime(2026, 10, 15, 12, 0, 0);
        var dadoDeBaja = CrearItem(proveedor.Id, "A-3", "Goma blanca");
        dadoDeBaja.FechaActualizacion = new DateTime(2026, 10, 20, 12, 0, 0);
        var deOtroProveedor = CrearItem(otroProveedor.Id, "B-1", "Regla");
        deOtroProveedor.FechaActualizacion = new DateTime(2026, 10, 25, 12, 0, 0);
        _context.CatalogosProveedores.AddRange(antiguo, reciente, dadoDeBaja, deOtroProveedor);
        await _context.SaveChangesAsync();
        dadoDeBaja.MarkAsDeleted();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act: la búsqueda solo trae el ítem antiguo, pero la fecha es la de la lista completa.
        var resultado = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto
        {
            IdProveedor = proveedor.Id,
            TerminoBusqueda = "Cuaderno",
            Pagina = 1,
            TamañoPagina = 50
        });

        // Assert: ni el ítem dado de baja ni el de otro proveedor cuentan.
        resultado.Items.Should().ContainSingle();
        resultado.FechaUltimaPlanilla.Should().Be(new DateTime(2026, 10, 15, 12, 0, 0));
    }

    [Fact]
    public async Task ObtenerCatalogoPaginadoAsync_ProveedorSinCatalogo_FechaUltimaPlanillaNula()
    {
        // Arrange
        var proveedor = await CrearProveedorAsync();

        // Act
        var resultado = await _sut.ObtenerCatalogoPaginadoAsync(new ConsultaCatalogoProveedorDto
        {
            IdProveedor = proveedor.Id,
            Pagina = 1,
            TamañoPagina = 50
        });

        // Assert
        resultado.FechaUltimaPlanilla.Should().BeNull();
    }
}
