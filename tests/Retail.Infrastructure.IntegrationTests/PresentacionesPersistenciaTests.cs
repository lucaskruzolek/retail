using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests;

/// <summary>
/// Restricciones de base de datos de las presentaciones derivadas (RF-21) contra LocalDB.
/// Los tests de rechazo asignan las propiedades directamente y saltan el Dominio a propósito: simulan un script
/// o una herramienta externa, que es lo que la defensa en profundidad del motor relacional debe atajar.
/// </summary>
public class PresentacionesPersistenciaTests : IAsyncLifetime, IDisposable
{
    private const string CheckPresentacion = "CK_ARTICULOS_Presentacion";

    private readonly string _dbName = $"RetailDb_Test_Present_{Guid.NewGuid():N}";
    private RetailDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        _context = new RetailDbContext(options);
        await _context.Database.MigrateAsync();
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
    public async Task Presentacion_Valida_SePersisteYSeRecuperaConSuOrigen()
    {
        // Arrange
        var origen = await GuardarOrigenAsync();
        var derivado = NuevoArticulo("Sobre manila (unidad)");
        derivado.DefinirComoPresentacionDe(origen, 100);
        _context.Articulos.Add(derivado);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var derivadoLeido = await _context.Articulos.AsNoTracking()
            .Include(a => a.ArticuloOrigen)
            .SingleAsync(a => a.Id == derivado.Id);
        var origenLeido = await _context.Articulos.AsNoTracking()
            .Include(a => a.Presentaciones)
            .SingleAsync(a => a.Id == origen.Id);

        // Assert
        derivadoLeido.UnidadesPorOrigen.Should().Be(100);
        derivadoLeido.CostoReposicion.Should().Be(10m);
        derivadoLeido.ArticuloOrigen!.Id.Should().Be(origen.Id);
        origenLeido.Presentaciones.Should().ContainSingle().Which.Id.Should().Be(derivado.Id);
    }

    [Fact]
    public async Task Presentacion_ConCatalogoProveedor_LaRechazaElCheck()
    {
        // Arrange
        var origen = await GuardarOrigenAsync();
        var catalogo = await GuardarCatalogoAsync("SOB-1");
        var derivado = NuevoArticulo("Unidad con catálogo");
        derivado.IdArticuloOrigen = origen.Id;
        derivado.UnidadesPorOrigen = 10;
        derivado.IdCatalogoProveedor = catalogo.Id;
        _context.Articulos.Add(derivado);

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, CheckPresentacion);
    }

    [Fact]
    public async Task Presentacion_ConUnidadesEnCero_LaRechazaElCheck()
    {
        // Arrange
        var origen = await GuardarOrigenAsync();
        var derivado = NuevoArticulo("Unidad x0");
        derivado.IdArticuloOrigen = origen.Id;
        derivado.UnidadesPorOrigen = 0;
        _context.Articulos.Add(derivado);

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, CheckPresentacion);
    }

    [Fact]
    public async Task Presentacion_OrigenSinUnidades_LaRechazaElCheck()
    {
        // Arrange
        var origen = await GuardarOrigenAsync();
        var derivado = NuevoArticulo("Unidad sin unidades");
        derivado.IdArticuloOrigen = origen.Id;
        derivado.UnidadesPorOrigen = null;
        _context.Articulos.Add(derivado);

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, CheckPresentacion);
    }

    [Fact]
    public async Task Presentacion_ComoServicio_LaRechazaElCheck()
    {
        // Arrange
        var origen = await GuardarOrigenAsync();
        var derivado = NuevoArticulo("Servicio derivado");
        derivado.IdArticuloOrigen = origen.Id;
        derivado.UnidadesPorOrigen = 10;
        derivado.EsServicio = true;
        _context.Articulos.Add(derivado);

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, CheckPresentacion);
    }

    [Fact]
    public async Task Presentacion_DeSiMisma_LaRechazaElCheck()
    {
        // Arrange: la FK se cumple (el Id existe), solo el CHECK puede rechazarlo
        var articulo = await GuardarOrigenAsync();
        articulo.IdArticuloOrigen = articulo.Id;
        articulo.UnidadesPorOrigen = 1;

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, CheckPresentacion);
    }

    [Fact]
    public async Task Presentacion_ConOrigenInexistente_LaRechazaLaFk()
    {
        // Arrange
        var derivado = NuevoArticulo("Unidad huérfana");
        derivado.IdArticuloOrigen = 999_999;
        derivado.UnidadesPorOrigen = 10;
        _context.Articulos.Add(derivado);

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, "FK_ARTICULOS_ARTICULOS_id_articulo_origen");
    }

    [Fact]
    public async Task Articulo_DosActivosVinculadosAlMismoItem_LoRechazaElIndiceUnico()
    {
        // Arrange
        var catalogo = await GuardarCatalogoAsync("SOB-1");
        var primero = NuevoArticulo("Sobre manila (pack)");
        primero.IdCatalogoProveedor = catalogo.Id;
        _context.Articulos.Add(primero);
        await _context.SaveChangesAsync();

        var segundo = NuevoArticulo("Sobre manila (otro pack)");
        segundo.IdCatalogoProveedor = catalogo.Id;
        _context.Articulos.Add(segundo);

        // Act
        var act = () => _context.SaveChangesAsync();

        // Assert
        await DebeRechazarseAsync(act, "IX_ARTICULOS_id_catalogo_proveedor");
    }

    [Fact]
    public async Task Articulo_VinculoDadoDeBaja_PermiteVincularOtroArticuloAlMismoItem()
    {
        // Arrange
        var catalogo = await GuardarCatalogoAsync("SOB-1");
        var anterior = NuevoArticulo("Sobre manila (pack discontinuado)");
        anterior.IdCatalogoProveedor = catalogo.Id;
        _context.Articulos.Add(anterior);
        await _context.SaveChangesAsync();

        anterior.MarkAsDeleted();
        await _context.SaveChangesAsync();

        var nuevo = NuevoArticulo("Sobre manila (pack nuevo)");
        nuevo.IdCatalogoProveedor = catalogo.Id;
        _context.Articulos.Add(nuevo);

        // Act
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Assert
        var vinculados = await _context.Articulos.AsNoTracking().IgnoreQueryFilters()
            .Where(a => a.IdCatalogoProveedor == catalogo.Id)
            .ToListAsync();
        vinculados.Should().HaveCount(2);
        vinculados.Should().ContainSingle(a => a.DeletedAt == null).Which.Id.Should().Be(nuevo.Id);
    }

    private static Articulo NuevoArticulo(string descripcion)
    {
        return new Articulo
        {
            Descripcion = descripcion,
            CostoReposicion = 1000m,
            PorcentajeGanancia = 40m,
            PrecioVenta = 1400m,
            StockActual = 5,
            StockMinimo = 1
        };
    }

    private async Task<Articulo> GuardarOrigenAsync()
    {
        var origen = NuevoArticulo("Sobre manila (pack x100)");
        _context.Articulos.Add(origen);
        await _context.SaveChangesAsync();
        return origen;
    }

    private async Task<CatalogoProveedor> GuardarCatalogoAsync(string codigo)
    {
        var proveedor = Proveedor.Crear("Distribuidora Sur", "30-11111111-8");
        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        var catalogo = new CatalogoProveedor
        {
            IdProveedor = proveedor.Id,
            CodigoProveedor = codigo,
            DescripcionProveedor = "Sobre manila x100",
            CostoReposicion = 1000m
        };
        _context.CatalogosProveedores.Add(catalogo);
        await _context.SaveChangesAsync();
        return catalogo;
    }

    /// <summary>
    /// Afirma que el motor rechazó el guardado y que el error nombra la restricción esperada,
    /// para no dar por buena una falla causada por otra regla.
    /// </summary>
    private static async Task DebeRechazarseAsync(Func<Task> accion, string restriccion)
    {
        var excepcion = await accion.Should().ThrowAsync<DbUpdateException>();
        excepcion.Which.InnerException.Should().NotBeNull();
        excepcion.Which.InnerException!.Message.Should().Contain(restriccion);
    }
}
