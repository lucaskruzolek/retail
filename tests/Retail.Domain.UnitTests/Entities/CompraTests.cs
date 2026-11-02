using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class CompraTests
{
    private const int IdProveedor = 1;
    private const int IdUsuario = 2;
    private const int IdArticulo = 10;

    // ── Método fábrica Registrar ──────────────────────────────────────────────

    [Fact]
    public void Registrar_DatosValidos_CreaCompraVaciaConEstadoIngresada()
    {
        // Act
        var compra = Compra.Registrar(IdProveedor, IdUsuario, "FACTURA", "0001-00000123", DateTime.UtcNow.AddDays(-1));

        // Assert
        compra.IdProveedor.Should().Be(IdProveedor);
        compra.IdUsuario.Should().Be(IdUsuario);
        compra.TipoComprobante.Should().Be("FACTURA");
        compra.NumeroComprobante.Should().Be("0001-00000123");
        compra.Detalles.Should().BeEmpty();
        compra.Subtotal.Should().Be(0m);
        compra.Total.Should().Be(0m);
        compra.Estado.Should().Be("INGRESADA");
        compra.IdPedidoOrigen.Should().BeNull();
    }

    [Fact]
    public void Registrar_ConPedidoOrigen_LoConserva()
    {
        // Act
        var compra = Compra.Registrar(
            IdProveedor,
            IdUsuario,
            "FACTURA",
            "0001-00000123",
            DateTime.UtcNow.AddDays(-1),
            idPedidoOrigen: 42);

        // Assert
        compra.IdPedidoOrigen.Should().Be(42);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Registrar_PedidoOrigenCero_LanzaExcepcion(int idPedidoOrigenInvalido)
    {
        // Act
        var act = () => Compra.Registrar(
            IdProveedor,
            IdUsuario,
            "FACTURA",
            "0001-00000123",
            DateTime.UtcNow.AddDays(-1),
            idPedidoOrigen: idPedidoOrigenInvalido);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, IdUsuario)]
    [InlineData(IdProveedor, 0)]
    public void Registrar_IdentificadoresInvalidos_LanzaArgumentOutOfRangeException(int idProveedor, int idUsuario)
    {
        // Act
        var act = () => Compra.Registrar(idProveedor, idUsuario, "FACTURA", "0001-00000001", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_TipoComprobanteVacio_LanzaArgumentException(string tipoComprobante)
    {
        // Act
        var act = () => Compra.Registrar(IdProveedor, IdUsuario, tipoComprobante, "0001-00000001", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Registrar_FechaEmisionFutura_LanzaDomainException()
    {
        // Act
        var act = () => Compra.Registrar(IdProveedor, IdUsuario, "FACTURA", "0001-00000001", DateTime.UtcNow.AddDays(10));

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*fecha de emisión*");
    }

    // ── AgregarDetalle ────────────────────────────────────────────────────────

    [Fact]
    public void AgregarDetalle_VariosRenglones_AcumulaSubtotalYTotalConRedondeo()
    {
        // Arrange
        var compra = CrearCompra();

        // Act — dos renglones con costos que implican redondeo
        compra.AgregarDetalle(IdArticulo, cantidad: 3, costoUnitario: 150.505m);  // subtotal = 451.53 (3 × 150.51 redondeado)
        compra.AgregarDetalle(IdArticulo + 1, cantidad: 1, costoUnitario: 99.994m); // subtotal = 99.99

        // Assert
        compra.Subtotal.Should().Be(551.52m);
        compra.Total.Should().Be(551.52m);
        compra.Detalles.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0, 1, 100)]
    [InlineData(IdArticulo, 0, 100)]
    [InlineData(IdArticulo, -1, 100)]
    [InlineData(IdArticulo, 1, 0)]
    [InlineData(IdArticulo, 1, -10)]
    public void AgregarDetalle_ValoresInvalidos_LanzaArgumentOutOfRangeException(
        int idArticulo,
        int cantidad,
        decimal costoUnitario)
    {
        // Arrange
        var compra = CrearCompra();

        // Act
        var act = () => compra.AgregarDetalle(idArticulo, cantidad, costoUnitario);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AgregarDetalle_MuchosRenglones_CalculaTotalCorrectamente()
    {
        // Arrange
        var compra = CrearCompra();

        // Act — 50 renglones para verificar consistencia acumulada
        for (var i = 1; i <= 50; i++)
        {
            compra.AgregarDetalle(idArticulo: i, cantidad: 2, costoUnitario: 10.00m);
        }

        // Assert — 50 × (2 × 10.00) = 1000.00
        compra.Subtotal.Should().Be(1000.00m);
        compra.Total.Should().Be(1000.00m);
        compra.Detalles.Should().HaveCount(50);
    }

    [Fact]
    public void Detalles_ColeccionEsDeSoloLectura_NoPermiteCastingAModificable()
    {
        // Arrange
        var compra = CrearCompra();
        compra.AgregarDetalle(IdArticulo, cantidad: 1, costoUnitario: 50m);

        // Assert
        compra.Detalles.Should().BeAssignableTo<IReadOnlyCollection<DetalleCompra>>();
    }

    // ── C-2: Artículo repetido ─────────────────────────────────────────────────

    [Fact]
    public void AgregarDetalle_ArticuloRepetido_LanzaDomainException()
    {
        // Arrange
        var compra = CrearCompra();
        compra.AgregarDetalle(IdArticulo, cantidad: 2, costoUnitario: 100m);

        // Act — intentar agregar el mismo artículo por segunda vez
        var act = () => compra.AgregarDetalle(IdArticulo, cantidad: 1, costoUnitario: 120m);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage($"*{IdArticulo}*");
    }

    [Fact]
    public void AgregarDetalle_ArticulosDiferentes_SeAgreganCorrectamente()
    {
        // Arrange
        var compra = CrearCompra();

        // Act — dos artículos distintos deben poder coexistir
        compra.AgregarDetalle(IdArticulo, cantidad: 2, costoUnitario: 100m);
        compra.AgregarDetalle(IdArticulo + 1, cantidad: 1, costoUnitario: 50m);

        // Assert
        compra.Detalles.Should().HaveCount(2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Compra CrearCompra() =>
        Compra.Registrar(IdProveedor, IdUsuario, "FACTURA", "0001-00000001", DateTime.UtcNow.AddDays(-1));
}
