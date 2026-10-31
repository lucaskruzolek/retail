using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Compras;
using Retail.Application.Services;
using Retail.Application.Validators.Compras;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.IntegrationTests.TestData;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Repositories;
using Retail.Infrastructure.Persistence.Services;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Services;

/// <summary>
/// Pruebas de integración para el ingreso de compras a proveedores (RF-19, RF-21).
/// Verifican la persistencia atómica en SQL Server de cabecera y renglones, el incremento de stock físico
/// y el recálculo automático de precios de reposición y venta (incluyendo la propagación a derivadas).
/// </summary>
public class RegistrarCompraIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbName = $"RetailDb_Test_Compras_{Guid.NewGuid():N}";
    private string _connectionString = null!;
    private RetailDbContext _context = null!;
    private CompraService _sut = null!;
    private int _idUsuario;
    private int _idProveedor;

    public async Task InitializeAsync()
    {
        _connectionString = ServidorDePruebas.CadenaDeConexion(_dbName);
        _context = CrearContexto();
        await _context.Database.MigrateAsync();
        _sut = CrearServicio(_context);

        var rol = new Rol { NombreRol = "Gerente", Descripcion = "Administrador del sistema" };
        _context.Roles.Add(rol);
        await _context.SaveChangesAsync();

        var usuario = Usuario.Crear("gerente1", "Gerente", "Principal", "hash", RolUsuarioEnum.Gerente, "gerente@libreria.com");
        _context.Usuarios.Add(usuario);

        var proveedor = Proveedor.Crear("Editorial Central S.A.", "30-12345678-1", "011-4321-8888", "ventas@central.com");
        _context.Proveedores.Add(proveedor);

        await _context.SaveChangesAsync();

        _idUsuario = usuario.Id;
        _idProveedor = proveedor.Id;
        _context.ChangeTracker.Clear();
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
    public async Task RegistrarCompraAsync_CompraValida_PersisteCompraDetallesStockYPrecios()
    {
        // Arrange
        var articulo = await GuardarArticuloAsync("Cuaderno A4 80h", costo: 100m, markup: 50m, precio: 150m, stock: 10);

        var dto = new CrearCompraDto
        {
            IdProveedor = _idProveedor,
            IdUsuario = _idUsuario,
            TipoComprobante = "FACTURA",
            NumeroComprobante = "0001-00000042",
            FechaEmision = DateTime.UtcNow.AddHours(-2),
            Items =
            [
                new DetalleCompraDto
                {
                    IdArticulo = articulo.Id,
                    Cantidad = 5,
                    CostoUnitario = 120m,
                }
            ],
        };

        // Act
        var resultado = await _sut.RegistrarCompraAsync(dto);
        _context.ChangeTracker.Clear();

        // Assert: cabecera de compra persistida
        resultado.IdCompra.Should().BePositive();
        resultado.Total.Should().Be(600m);
        resultado.ArticulosActualizados.Should().Be(1);

        var compraEnDb = await _context.Compras.AsNoTracking()
            .Include(c => c.Detalles)
            .SingleAsync(c => c.Id == resultado.IdCompra);

        compraEnDb.IdProveedor.Should().Be(_idProveedor);
        compraEnDb.IdUsuario.Should().Be(_idUsuario);
        compraEnDb.TipoComprobante.Should().Be("FACTURA");
        compraEnDb.NumeroComprobante.Should().Be("0001-00000042");
        compraEnDb.Subtotal.Should().Be(600m);
        compraEnDb.Total.Should().Be(600m);
        compraEnDb.Detalles.Should().HaveCount(1);

        var detalle = compraEnDb.Detalles.Single();
        detalle.IdArticulo.Should().Be(articulo.Id);
        detalle.Cantidad.Should().Be(5);
        detalle.CostoUnitario.Should().Be(120m);
        detalle.SubtotalItem.Should().Be(600m);

        // Assert: stock y precios del artículo actualizados en la base de datos
        var articuloEnDb = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == articulo.Id);
        articuloEnDb.StockActual.Should().Be(15, "el stock anterior (10) se incrementó en 5 unidades");
        articuloEnDb.CostoReposicion.Should().Be(120m, "el nuevo costo unitario de compra reemplaza el anterior");
        articuloEnDb.PrecioVenta.Should().Be(180m, "el precio se recalculó con el markup del 50%: 120 * 1.50 = 180");
    }

    [Fact]
    public async Task RegistrarCompraAsync_ConPresentacionesDerivadas_PropagaNuevoCostoAPresentacionesHijas()
    {
        // Arrange: artículo origen (caja) y artículo derivado (unidad suelta, 4 unidades por caja)
        var cajaLapiceras = await GuardarArticuloAsync("Caja Lapiceras x4", costo: 100m, markup: 50m, precio: 150m, stock: 8);

        var lapiceraSuelta = new Articulo
        {
            Descripcion = "Lapicera Suelta",
            CostoReposicion = 25m,
            PorcentajeGanancia = 60m,
            PrecioVenta = 40m,
            StockActual = 20,
            StockMinimo = 5,
            IdArticuloOrigen = cajaLapiceras.Id,
            UnidadesPorOrigen = 4,
        };
        _context.Articulos.Add(lapiceraSuelta);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Compra de 2 cajas con aumento de costo a 160m
        var dto = new CrearCompraDto
        {
            IdProveedor = _idProveedor,
            IdUsuario = _idUsuario,
            TipoComprobante = "REMITO",
            NumeroComprobante = "0002-00000100",
            FechaEmision = DateTime.UtcNow.AddHours(-1),
            Items =
            [
                new DetalleCompraDto
                {
                    IdArticulo = cajaLapiceras.Id,
                    Cantidad = 2,
                    CostoUnitario = 160m,
                }
            ],
        };

        // Act
        var resultado = await _sut.RegistrarCompraAsync(dto);
        _context.ChangeTracker.Clear();

        // Assert
        resultado.ArticulosActualizados.Should().Be(1);

        var cajaEnDb = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == cajaLapiceras.Id);
        cajaEnDb.StockActual.Should().Be(10);
        cajaEnDb.CostoReposicion.Should().Be(160m);
        cajaEnDb.PrecioVenta.Should().Be(240m); // 160 * 1.50

        var derivadaEnDb = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == lapiceraSuelta.Id);
        derivadaEnDb.CostoReposicion.Should().Be(40m, "160m / 4 unidades por caja = 40m");
        derivadaEnDb.PrecioVenta.Should().Be(64m, "40m * 1.60 = 64m");
    }

    // ── C-1: Índice único de comprobante (defensa en profundidad) ─────────────

    [Fact]
    public async Task IndiceUnicoComprobante_MismoComprobanteRegistradoDosVeces_LanzaRestriccionDeUnicidad()
    {
        // Arrange: dos compras con el mismo proveedor, tipo y número de comprobante.
        var articulo1 = await GuardarArticuloAsync("Lápiz HB", costo: 50m, markup: 40m, precio: 70m, stock: 20);
        var articulo2 = await GuardarArticuloAsync("Lápiz 2B", costo: 60m, markup: 40m, precio: 84m, stock: 10);

        var dtoOriginal = new CrearCompraDto
        {
            IdProveedor = _idProveedor,
            IdUsuario = _idUsuario,
            TipoComprobante = "FACTURA",
            NumeroComprobante = "0001-99999999",
            FechaEmision = DateTime.UtcNow.AddHours(-3),
            Items = [new DetalleCompraDto { IdArticulo = articulo1.Id, Cantidad = 1, CostoUnitario = 55m }],
        };

        // Primera compra: se registra correctamente.
        await _sut.RegistrarCompraAsync(dtoOriginal);

        // Segunda compra con el mismo comprobante pero diferente artículo: viola el índice.
        // Omitimos la verificación previa del servicio usando un contexto nuevo y el repositorio directo,
        // para probar que el índice en la base es la defensa en profundidad (C-1).
        var contextoSegundo = CrearContexto();
        var servicioSegundo = CrearServicio(contextoSegundo);
        var compraDirecta = Domain.Entities.Compra.Registrar(
            _idProveedor, _idUsuario, "FACTURA", "0001-99999999", DateTime.UtcNow.AddHours(-2));
        compraDirecta.AgregarDetalle(articulo2.Id, 1, 65m);
        contextoSegundo.Compras.Add(compraDirecta);

        // Act
        var act = () => contextoSegundo.SaveChangesAsync();

        // Assert — el índice UX_COMPRAS_Comprobante frena la inserción.
        await act.Should().ThrowAsync<Exception>()
            .Where(ex => ex.Message.Contains("UX_COMPRAS_Comprobante") ||
                         ex.Message.Contains("unique") ||
                         ex.Message.Contains("UNIQUE") ||
                         ex.GetType().Name.Contains("Unicidad") ||
                         ex.GetType().Name.Contains("DbUpdateException"));

        await contextoSegundo.DisposeAsync();

        // El stock del segundo artículo no debe haber cambiado.
        _context.ChangeTracker.Clear();
        var articulo2EnDb = await _context.Articulos.AsNoTracking().SingleAsync(a => a.Id == articulo2.Id);
        articulo2EnDb.StockActual.Should().Be(10, "la segunda compra fue rechazada antes de modificar el stock");
    }

    private RetailDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<RetailDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new RetailDbContext(options);
    }

    private static CompraService CrearServicio(RetailDbContext context)
    {
        return new CompraService(
            new Repository<Compra>(context),
            new Repository<Articulo>(context),
            new Repository<Proveedor>(context),
            new Repository<Usuario>(context),
            new UnitOfWork(context),
            new CrearCompraValidator());
    }

    private async Task<Articulo> GuardarArticuloAsync(string descripcion, decimal costo, decimal markup, decimal precio, int stock)
    {
        var articulo = new Articulo
        {
            Descripcion = descripcion,
            CostoReposicion = costo,
            PorcentajeGanancia = markup,
            PrecioVenta = precio,
            StockActual = stock,
            StockMinimo = 2,
        };
        _context.Articulos.Add(articulo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return articulo;
    }
}
