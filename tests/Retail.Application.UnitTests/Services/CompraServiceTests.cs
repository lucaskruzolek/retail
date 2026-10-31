using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation;
using NSubstitute;
using Retail.Application.DTOs.Compras;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Services;
using Retail.Application.Validators.Compras;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Application.UnitTests.Services;

public class CompraServiceTests
{
    private const int IdProveedor = 1;
    private const int IdUsuario = 2;
    private const int IdArticulo = 10;

    private readonly IRepository<Compra> _compraRepository;
    private readonly IRepository<Articulo> _articuloRepository;
    private readonly IRepository<Proveedor> _proveedorRepository;
    private readonly IRepository<Usuario> _usuarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CompraService _sut;

    // Proveedor y usuario activos configurados por defecto en cada test.
    private readonly Proveedor _proveedor;
    private readonly Usuario _usuario;

    public CompraServiceTests()
    {
        _compraRepository = Substitute.For<IRepository<Compra>>();
        _articuloRepository = Substitute.For<IRepository<Articulo>>();
        _proveedorRepository = Substitute.For<IRepository<Proveedor>>();
        _usuarioRepository = Substitute.For<IRepository<Usuario>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _proveedor = Proveedor.Crear("Editorial Norte S.A.", "20123456786");
        _proveedor.Id = IdProveedor;
        _proveedorRepository.GetByIdAsync(IdProveedor, Arg.Any<CancellationToken>()).Returns(_proveedor);

        _usuario = Usuario.Crear("operador", "Juan", "Perez", "hash123", RolUsuarioEnum.Cajero, null);
        _usuario.Id = IdUsuario;
        _usuarioRepository.GetByIdAsync(IdUsuario, Arg.Any<CancellationToken>()).Returns(_usuario);

        // Por defecto no hay presentaciones derivadas.
        _articuloRepository
            .FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Articulo>());

        _sut = new CompraService(
            _compraRepository,
            _articuloRepository,
            _proveedorRepository,
            _usuarioRepository,
            _unitOfWork,
            new CrearCompraValidator());
    }

    // ── Compra exitosa y persistencia ─────────────────────────────────────────

    [Fact]
    public async Task RegistrarCompraAsync_CompraValida_IncrementaStockYGuardaUnaSolaVez()
    {
        // Arrange
        var lapiz = CrearArticulo(IdArticulo, "Lapicera", costo: 100m, precio: 150m, stock: 5);
        ConfigurarArticulos(lapiz);

        var dto = CrearDtoValido([Item(lapiz, 3, costoUnitario: 100m)]);

        // Act
        var resultado = await _sut.RegistrarCompraAsync(dto);

        // Assert
        lapiz.StockActual.Should().Be(8); // 5 + 3
        await _compraRepository.Received(1).AddAsync(Arg.Any<Compra>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        resultado.IdProveedor.Should().Be(IdProveedor);
        resultado.TipoComprobante.Should().Be("FACTURA");
    }

    [Fact]
    public async Task RegistrarCompraAsync_CostoCambia_ActualizaPrecioDeVentaDelArticulo()
    {
        // Arrange — costo actual del artículo = 100, se compra a 130 (precio sube)
        var cuaderno = CrearArticulo(IdArticulo, "Cuaderno A4", costo: 100m, precio: 150m, stock: 10, markup: 50m);
        ConfigurarArticulos(cuaderno);

        var dto = CrearDtoValido([Item(cuaderno, 5, costoUnitario: 130m)]);

        // Act
        await _sut.RegistrarCompraAsync(dto);

        // Assert: el dominio recalcula PrecioVenta = 130 * (1 + 50/100) = 195
        cuaderno.CostoReposicion.Should().Be(130m);
        cuaderno.PrecioVenta.Should().Be(195m);
    }

    [Fact]
    public async Task RegistrarCompraAsync_CostoNoCambia_NoModificaPrecioDeVenta()
    {
        // Arrange — se compra al mismo costo que ya tenía el artículo
        var cuaderno = CrearArticulo(IdArticulo, "Cuaderno A4", costo: 100m, precio: 150m, stock: 10, markup: 50m);
        ConfigurarArticulos(cuaderno);

        var dto = CrearDtoValido([Item(cuaderno, 5, costoUnitario: 100m)]);

        // Act
        await _sut.RegistrarCompraAsync(dto);

        // Assert: el precio no cambió.
        cuaderno.PrecioVenta.Should().Be(150m);
    }

    [Fact]
    public async Task RegistrarCompraAsync_CostoCambiaConDerivadas_PropagaCostoAPresentaciones()
    {
        // Arrange
        var pack = CrearArticulo(IdArticulo, "Pack 12u lapiceras", costo: 120m, precio: 180m, stock: 20, markup: 50m);
        // La presentación derivada (unidad suelta) se relaciona con el pack (4 unidades por origen).
        var unitaria = CrearArticuloDerivado(id: 20, descripcion: "Lapicera unitaria", idOrigen: pack.Id, unidadesPorOrigen: 4, markup: 60m);
        ConfigurarArticulosYDerivadas([pack], [unitaria]);

        var dto = CrearDtoValido([Item(pack, cantidad: 2, costoUnitario: 160m)]);

        // Act
        await _sut.RegistrarCompraAsync(dto);

        // Assert: pack actualizado a 160; derivada = 160 / 4 = 40, precio = 40 * 1.60 = 64.
        pack.CostoReposicion.Should().Be(160m);
        unitaria.CostoReposicion.Should().Be(40m);
        unitaria.PrecioVenta.Should().Be(64m);
        await _articuloRepository.Received(1).UpdateAsync(unitaria, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegistrarCompraAsync_ProveedorInexistente_LanzaDomainException()
    {
        // Arrange
        _proveedorRepository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Proveedor?)null);
        var dto = CrearDtoValido([Item(IdArticulo, cantidad: 1, costoUnitario: 50m)]) with { IdProveedor = 99 };

        // Act
        var act = () => _sut.RegistrarCompraAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*proveedor*");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegistrarCompraAsync_ArticuloEsServicio_LanzaDomainException()
    {
        // Arrange
        var servicio = CrearArticulo(IdArticulo, "Consultoría", costo: 0m, precio: 500m, stock: 0, esServicio: true);
        ConfigurarArticulos(servicio);

        var dto = CrearDtoValido([Item(IdArticulo, cantidad: 1, costoUnitario: 500m)]);

        // Act
        var act = () => _sut.RegistrarCompraAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*servicio*");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegistrarCompraAsync_DtoSinItems_LanzaValidationExceptionSinConsultarLaBase()
    {
        // Arrange
        var dto = CrearDtoValido([]);

        // Act
        var act = () => _sut.RegistrarCompraAsync(dto);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _proveedorRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegistrarCompraAsync_TotalesDelResultado_ReflexanSubtotalYTotalCorrectamente()
    {
        // Arrange
        var art1 = CrearArticulo(10, "Artículo A", costo: 100m, precio: 150m, stock: 20);
        var art2 = CrearArticulo(11, "Artículo B", costo: 200m, precio: 300m, stock: 10);
        ConfigurarArticulos(art1, art2);

        var dto = CrearDtoValido(
        [
            Item(art1, 2, costoUnitario: 100m),  // 200
            Item(art2, 3, costoUnitario: 200m),  // 600
        ]);

        // Act
        var resultado = await _sut.RegistrarCompraAsync(dto);

        // Assert
        resultado.Subtotal.Should().Be(800m);
        resultado.Total.Should().Be(800m);
    }

    // ── C-1: Comprobante duplicado ────────────────────────────────────────────

    [Fact]
    public async Task RegistrarCompraAsync_ComprobanteYaRegistrado_LanzaExcepcion()
    {
        // Arrange — el repositorio devuelve una compra con el mismo comprobante
        var compraExistente = Compra.Registrar(IdProveedor, IdUsuario, "FACTURA", "0001-00000001", DateTime.UtcNow.AddDays(-2));
        _compraRepository
            .FindAsync(Arg.Any<Expression<Func<Compra, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Compra> { compraExistente }.AsReadOnly());

        var dto = CrearDtoValido([Item(IdArticulo, cantidad: 1, costoUnitario: 50m)]);

        // Act
        var act = () => _sut.RegistrarCompraAsync(dto);

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*ya fue registrado*");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    // ── C-4: DescartarCambios después de guardar ──────────────────────────────

    [Fact]
    public async Task RegistrarCompraAsync_GuardadoExitoso_DescartaCambiosDespuesDeGuardar()
    {
        // Arrange
        var lapiz = CrearArticulo(IdArticulo, "Lapicera", costo: 100m, precio: 150m, stock: 5);
        ConfigurarArticulos(lapiz);

        var dto = CrearDtoValido([Item(lapiz, 3, costoUnitario: 100m)]);

        // Act
        await _sut.RegistrarCompraAsync(dto);

        // Assert — DescartarCambios debe llamarse exactamente una vez después del guardado exitoso (D-15).
        _unitOfWork.Received(1).DescartarCambios();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Articulo CrearArticulo(
        int id,
        string descripcion,
        decimal costo,
        decimal precio,
        int stock,
        decimal markup = 50m,
        bool esServicio = false)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = descripcion,
            CostoReposicion = costo,
            PrecioVenta = precio,
            StockActual = stock,
            PorcentajeGanancia = markup,
            EsServicio = esServicio,
        };
    }

    private static Articulo CrearArticuloDerivado(
        int id,
        string descripcion,
        int idOrigen,
        int unidadesPorOrigen,
        decimal markup)
    {
        return new Articulo
        {
            Id = id,
            Descripcion = descripcion,
            IdArticuloOrigen = idOrigen,
            UnidadesPorOrigen = unidadesPorOrigen,
            PorcentajeGanancia = markup,
            CostoReposicion = 0m,
            PrecioVenta = 0m,
        };
    }

    private void ConfigurarArticulos(params Articulo[] articulos)
    {
        // Primera llamada FindAsync: verificación de comprobante duplicado → sin resultados.
        // Segunda llamada FindAsync: artículos comprados.
        // Tercera llamada FindAsync: presentaciones derivadas → vacías.
        _articuloRepository
            .FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
                ci => (IReadOnlyList<Articulo>)articulos,
                ci => Array.Empty<Articulo>());   // segunda llamada: presentaciones derivadas vacías

        // La primera llamada de FindAsync al _compraRepository simula que no hay comprobante previo.
        _compraRepository
            .FindAsync(Arg.Any<Expression<Func<Compra, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Compra>());
    }

    private void ConfigurarArticulosYDerivadas(Articulo[] articulos, Articulo[] derivadas)
    {
        _articuloRepository
            .FindAsync(Arg.Any<Expression<Func<Articulo, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
                ci => (IReadOnlyList<Articulo>)articulos,
                ci => (IReadOnlyList<Articulo>)derivadas);

        _compraRepository
            .FindAsync(Arg.Any<Expression<Func<Compra, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Compra>());
    }

    private static DetalleCompraDto Item(Articulo articulo, int cantidad, decimal costoUnitario) =>
        new() { IdArticulo = articulo.Id, Cantidad = cantidad, CostoUnitario = costoUnitario };

    private static DetalleCompraDto Item(int idArticulo, int cantidad, decimal costoUnitario) =>
        new() { IdArticulo = idArticulo, Cantidad = cantidad, CostoUnitario = costoUnitario };

    private static CrearCompraDto CrearDtoValido(IReadOnlyList<DetalleCompraDto> items) =>
        new()
        {
            IdProveedor = IdProveedor,
            IdUsuario = IdUsuario,
            TipoComprobante = "FACTURA",
            NumeroComprobante = "0001-00000001",
            FechaEmision = DateTime.UtcNow.AddDays(-1),
            Items = items,
        };
}
