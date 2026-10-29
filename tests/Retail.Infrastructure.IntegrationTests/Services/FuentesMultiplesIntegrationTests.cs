using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Services;
using Retail.Application.Validators.Articulos;
using Retail.Application.Validators.Proveedores;
using Retail.Domain.Entities;
using Retail.Infrastructure.ExternalServices.Excel;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Fuentes múltiples de punta a punta contra SQL Server Express (Track 4.3, L3a): los servicios reales sobre un mismo
/// contexto, como en una pantalla. Verifica lo que los tests unitarios no pueden: que EF Core resuelva el ciclo entre
/// ARTICULOS.id_catalogo_proveedor y CATALOGOS_PROVEEDORES.id_articulo al guardar, y que lo guardado respete el
/// invariante de A-16 (la principal es una de las fuentes).
/// </summary>
public class FuentesMultiplesIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Fuentes_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;
    private ProveedorService _proveedores = null!;
    private InventarioService _inventario = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(ServidorDePruebas.CadenaDeConexion(_dbName))
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();

        var catalogos = new CatalogoProveedorQueryService(_context);
        var articulos = new Repository<Articulo>(_context);
        var unitOfWork = new UnitOfWork(_context);

        _proveedores = new ProveedorService(
            new Repository<Proveedor>(_context),
            catalogos,
            articulos,
            unitOfWork,
            new ExcelCatalogParser(),
            new CrearProveedorValidator(),
            new ActualizarProveedorValidator(),
            new MapeoColumnasValidator(),
            new IncorporarCatalogoArticulosValidator());

        _inventario = new InventarioService(
            articulos,
            new Repository<Categoria>(_context),
            new Repository<Marca>(_context),
            unitOfWork,
            new CrearArticuloValidator(),
            new ActualizarArticuloValidator(),
            new CrearPresentacionValidator(),
            new FraccionarValidator(),
            catalogos);
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
    public async Task IncorporarArticulosATiendaAsync_ItemLibre_GuardaElItemComoFuenteYPrincipalDelArticuloNuevo()
    {
        // Arrange
        var (sur, _) = await SembrarItemsAsync();

        // Act: el artículo no tiene id hasta el INSERT; el ítem lo recibe por la navegación
        await IncorporarAsync(sur);

        // Assert
        var articulo = await LeerArticuloAsync("Edding 700 (Sur)");
        articulo.IdCatalogoProveedor.Should().Be(sur);
        articulo.CostoReposicion.Should().Be(800m);
        (await LeerFuentesAsync(articulo.Id)).Should().Equal(sur);
    }

    [Fact]
    public async Task VincularYDesvincular_FuenteAdicionalYCambioDePrincipal_MantienenElInvariante()
    {
        // Arrange: el ítem del proveedor Sur es la principal del artículo
        var (sur, norte) = await SembrarItemsAsync();
        await IncorporarAsync(sur);
        var idArticulo = (await LeerArticuloAsync("Edding 700 (Sur)")).Id;

        // Act 1: Norte se vincula como adicional; no toca el costo (A-2)
        await _proveedores.VincularArticuloACatalogoAsync(idArticulo, norte, comoPrincipal: false);
        var trasAdicional = await LeerArticuloAsync("Edding 700 (Sur)");

        // Act 2: Norte pasa a ser la principal; Sur sigue siendo fuente
        await _proveedores.VincularArticuloACatalogoAsync(idArticulo, norte, comoPrincipal: true);
        var trasCambio = await LeerArticuloAsync("Edding 700 (Sur)");

        // Act 3: se desvincula la principal; el artículo queda sin principal (J5)
        await _proveedores.DesvincularFuenteAsync(norte);
        var trasDesvincular = await LeerArticuloAsync("Edding 700 (Sur)");

        // Assert
        trasAdicional.IdCatalogoProveedor.Should().Be(sur);
        trasAdicional.CostoReposicion.Should().Be(800m);

        trasCambio.IdCatalogoProveedor.Should().Be(norte);
        trasCambio.CostoReposicion.Should().Be(750m);

        trasDesvincular.IdCatalogoProveedor.Should().BeNull();
        trasDesvincular.CostoReposicion.Should().Be(750m);
        (await LeerFuentesAsync(idArticulo)).Should().Equal(sur);
    }

    [Fact]
    public async Task BajaArticuloAsync_ConFuentes_LasLiberaYElItemSePuedeVolverAIncorporar()
    {
        // Arrange
        var (sur, norte) = await SembrarItemsAsync();
        await IncorporarAsync(sur);
        var idArticulo = (await LeerArticuloAsync("Edding 700 (Sur)")).Id;
        await _proveedores.VincularArticuloACatalogoAsync(idArticulo, norte, comoPrincipal: false);

        // Act (J3)
        await _inventario.BajaArticuloAsync(idArticulo);
        var resultado = await IncorporarAsync(sur);

        // Assert
        (await LeerFuentesAsync(idArticulo)).Should().BeEmpty();
        resultado.Incorporados.Should().Be(1);
    }

    /// <summary>
    /// Dos proveedores con un ítem cada uno, sin vincular: Sur (costo 800) y Norte (costo 750).
    /// </summary>
    private async Task<(int Sur, int Norte)> SembrarItemsAsync()
    {
        var proveedorSur = Proveedor.Crear("Distribuidora Sur", "30712345671");
        var proveedorNorte = Proveedor.Crear("Mayorista Norte", "30500010912");
        var itemSur = new CatalogoProveedor { Proveedor = proveedorSur, CodigoProveedor = "EDD-700", DescripcionProveedor = "Edding 700 (Sur)", CostoReposicion = 800m };
        var itemNorte = new CatalogoProveedor { Proveedor = proveedorNorte, CodigoProveedor = "MN-700", DescripcionProveedor = "Edding 700 (Norte)", CostoReposicion = 750m };
        _context.AddRange(itemSur, itemNorte);
        await _context.SaveChangesAsync();
        return (itemSur.Id, itemNorte.Id);
    }

    private Task<ResultadoIncorporacionDto> IncorporarAsync(int idCatalogo)
    {
        return _proveedores.IncorporarArticulosATiendaAsync(new IncorporarCatalogoArticulosDto
        {
            Items = [new ItemIncorporacionArticuloDto { IdCatalogo = idCatalogo, PorcentajeGanancia = 50m }]
        });
    }

    private async Task<Articulo> LeerArticuloAsync(string descripcion)
    {
        return await _context.Articulos.AsNoTracking().SingleAsync(a => a.Descripcion == descripcion);
    }

    private async Task<List<int>> LeerFuentesAsync(int idArticulo)
    {
        return await _context.CatalogosProveedores
            .AsNoTracking()
            .Where(c => c.IdArticulo == idArticulo)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .ToListAsync();
    }
}
