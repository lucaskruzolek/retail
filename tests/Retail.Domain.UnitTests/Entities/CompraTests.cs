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
        compra.Detalles.Should().HaveCount(2);
        compra.Subtotal.Should().Be(551.52m);
        compra.Total.Should().Be(551.52m);
    }

    [Theory]
    [InlineData(0, 1, 100)]
    [InlineData(IdArticulo, 0, 100)]
    [InlineData(IdArticulo, -1, 100)]
    public void AgregarDetalle_DatosInvalidos_LanzaArgumentOutOfRangeException(int idArticulo, int cantidad, decimal costo)
    {
        // Arrange
        var compra = CrearCompra();

        // Act
        var act = () => compra.AgregarDetalle(idArticulo, cantidad, costo);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AgregarDetalle_CostoCero_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var compra = CrearCompra();

        // Act
        var act = () => compra.AgregarDetalle(IdArticulo, cantidad: 1, costoUnitario: 0m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("costoUnitario");
    }

    [Fact]
    public void AgregarDetalle_UnRenglon_DetalleContieneValoresCorrectos()
    {
        // Arrange
        var compra = CrearCompra();

        // Act
        compra.AgregarDetalle(IdArticulo, cantidad: 5, costoUnitario: 200m);

        // Assert
        var detalle = compra.Detalles.Single();
        detalle.IdArticulo.Should().Be(IdArticulo);
        detalle.Cantidad.Should().Be(5);
        detalle.CostoUnitario.Should().Be(200m);
        detalle.SubtotalItem.Should().Be(1000m);
    }

    // ── Colección inmutable ───────────────────────────────────────────────────

    [Fact]
    public void Detalles_EsReadOnly_NoPermiteAgregarExternamente()
    {
        // Arrange
        var compra = CrearCompra();

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
