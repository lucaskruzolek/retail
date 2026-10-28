using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class PedidoTests
{
    private static Pedido Borrador() => Pedido.CrearBorrador(1, 2);

    private static Pedido BorradorConUnRenglon()
    {
        var pedido = Borrador();
        pedido.AgregarRenglon(10, 100, "LAP-01", "Lápiz negro", 5, 20m);
        return pedido;
    }

    private static Pedido Emitido()
    {
        var pedido = BorradorConUnRenglon();
        pedido.Emitir();
        return pedido;
    }

    // ───────────────────── Creación ─────────────────────

    [Fact]
    public void CrearBorrador_IdsValidos_QuedaEnBorrador()
    {
        // Act
        var pedido = Pedido.CrearBorrador(3, 4);

        // Assert
        pedido.Estado.Should().Be(EstadoPedidoEnum.Borrador);
        pedido.IdProveedor.Should().Be(3);
        pedido.IdUsuario.Should().Be(4);
        pedido.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        pedido.Detalles.Should().BeEmpty();
        pedido.FechaEmision.Should().BeNull();
        pedido.FechaCierre.Should().BeNull();
        pedido.TotalEstimado.Should().Be(0m);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void CrearBorrador_IdInvalido_LanzaArgumentOutOfRange(int idProveedor, int idUsuario)
    {
        var act = () => Pedido.CrearBorrador(idProveedor, idUsuario);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ───────────────────── Renglones ─────────────────────

    [Fact]
    public void AgregarRenglon_ItemNuevo_AgregaRenglonConFotoNormalizada()
    {
        // Arrange
        var pedido = Borrador();

        // Act
        pedido.AgregarRenglon(10, 100, "  LAP-01 ", " Lápiz   negro\tHB ", 5, 20m);

        // Assert
        var renglon = pedido.Detalles.Should().ContainSingle().Subject;
        renglon.IdArticulo.Should().Be(10);
        renglon.IdCatalogoProveedor.Should().Be(100);
        renglon.CodigoProveedor.Should().Be("LAP-01");
        renglon.Descripcion.Should().Be("Lápiz negro HB");
        renglon.Cantidad.Should().Be(5);
        renglon.CostoPactado.Should().Be(20m);
        renglon.SubtotalEstimado.Should().Be(100m);
    }

    [Fact]
    public void AgregarRenglon_ItemYaPresente_SumaCantidadYActualizaFoto()
    {
        // Arrange
        var pedido = BorradorConUnRenglon();

        // Act
        pedido.AgregarRenglon(10, 100, "LAP-01B", "Lápiz negro HB", 3, 25m);

        // Assert
        var renglon = pedido.Detalles.Should().ContainSingle().Subject;
        renglon.Cantidad.Should().Be(8);
        renglon.CodigoProveedor.Should().Be("LAP-01B");
        renglon.Descripcion.Should().Be("Lápiz negro HB");
        renglon.CostoPactado.Should().Be(25m);
    }

    [Fact]
    public void AgregarRenglon_ItemYaPresenteConOtroArticulo_LanzaDomainException()
    {
        var pedido = BorradorConUnRenglon();

        var act = () => pedido.AgregarRenglon(11, 100, "LAP-01", "Lápiz negro", 1, 20m);

        act.Should().Throw<DomainException>().WithMessage("*otro artículo*");
        pedido.Detalles.Single().Cantidad.Should().Be(5);
    }

    [Fact]
    public void AgregarRenglon_PedidoEmitido_LanzaTransicionInvalida()
    {
        var pedido = Emitido();

        var act = () => pedido.AgregarRenglon(11, 101, "GOM-01", "Goma", 1, 5m);

        act.Should().Throw<TransicionPedidoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPedidoEnum.Emitido);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AgregarRenglon_CantidadCero_LanzaExcepcion(int cantidad)
    {
        var pedido = Borrador();

        var act = () => pedido.AgregarRenglon(10, 100, "LAP-01", "Lápiz negro", cantidad, 20m);

        act.Should().Throw<DomainException>().WithMessage("*cantidad*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AgregarRenglon_CostoCero_LanzaDomainException(int costo)
    {
        var pedido = Borrador();

        var act = () => pedido.AgregarRenglon(10, 100, "LAP-01", "Lápiz negro", 1, costo);

        act.Should().Throw<DomainException>().WithMessage("*costo*");
    }

    [Theory]
    [InlineData("", "Lápiz negro")]
    [InlineData("   ", "Lápiz negro")]
    [InlineData("LAP-01", "")]
    [InlineData("LAP-01", "   ")]
    public void AgregarRenglon_TextoVacio_LanzaDomainException(string codigo, string descripcion)
    {
        var pedido = Borrador();

        var act = () => pedido.AgregarRenglon(10, 100, codigo, descripcion, 1, 20m);

        act.Should().Throw<DomainException>().WithMessage("Debe indicarse*");
    }

    [Fact]
    public void AgregarRenglon_DescripcionDemasiadoLarga_LanzaDomainException()
    {
        var pedido = Borrador();
        var descripcion = new string('a', Pedido.LongitudMaximaDescripcion + 1);

        var act = () => pedido.AgregarRenglon(10, 100, "LAP-01", descripcion, 1, 20m);

        act.Should().Throw<DomainException>().WithMessage("*supera los 200*");
    }

    [Fact]
    public void CambiarCantidad_RenglonExistente_ReemplazaCantidad()
    {
        var pedido = BorradorConUnRenglon();

        pedido.CambiarCantidad(100, 12);

        pedido.Detalles.Single().Cantidad.Should().Be(12);
    }

    [Fact]
    public void CambiarCantidad_RenglonInexistente_LanzaDomainException()
    {
        var pedido = BorradorConUnRenglon();

        var act = () => pedido.CambiarCantidad(999, 3);

        act.Should().Throw<DomainException>().WithMessage("*no figura*");
    }

    [Fact]
    public void QuitarRenglon_UnicoRenglon_DejaBorradorVacioSinCancelar()
    {
        var pedido = BorradorConUnRenglon();

        pedido.QuitarRenglon(100);

        pedido.Detalles.Should().BeEmpty();
        pedido.Estado.Should().Be(EstadoPedidoEnum.Borrador);
    }

    [Fact]
    public void QuitarRenglon_PedidoEmitido_LanzaTransicionInvalida()
    {
        var pedido = Emitido();

        var act = () => pedido.QuitarRenglon(100);

        act.Should().Throw<TransicionPedidoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPedidoEnum.Emitido);
    }

    // ───────────────────── Emisión ─────────────────────

    [Fact]
    public void Emitir_SinRenglones_LanzaExcepcion()
    {
        var pedido = Borrador();

        var act = () => pedido.Emitir();

        act.Should().Throw<DomainException>().WithMessage("*sin renglones*");
        pedido.Estado.Should().Be(EstadoPedidoEnum.Borrador);
    }

    [Fact]
    public void Emitir_ConRenglones_PasaAEmitidoYRegistraFecha()
    {
        var pedido = BorradorConUnRenglon();

        pedido.Emitir();

        pedido.Estado.Should().Be(EstadoPedidoEnum.Emitido);
        pedido.FechaEmision.Should().NotBeNull();
        pedido.FechaEmision!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        pedido.FechaCierre.Should().BeNull();
    }

    [Fact]
    public void Emitir_PedidoYaEmitido_LanzaTransicionInvalida()
    {
        var pedido = Emitido();

        var act = () => pedido.Emitir();

        act.Should().Throw<TransicionPedidoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPedidoEnum.Emitido);
    }

    // ───────────────────── Recepción ─────────────────────

    [Fact]
    public void MarcarRecibido_PedidoEmitido_PasaARecibido()
    {
        var pedido = Emitido();

        pedido.MarcarRecibido();

        pedido.Estado.Should().Be(EstadoPedidoEnum.Recibido);
        pedido.FechaCierre.Should().NotBeNull();
        pedido.FechaCierre!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarcarRecibido_PedidoEnBorrador_LanzaTransicionInvalida()
    {
        var pedido = BorradorConUnRenglon();

        var act = () => pedido.MarcarRecibido();

        act.Should().Throw<TransicionPedidoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPedidoEnum.Borrador);
    }

    [Fact]
    public void MarcarRecibido_PedidoCancelado_LanzaTransicionInvalida()
    {
        var pedido = Emitido();
        pedido.Cancelar("sin stock");

        var act = () => pedido.MarcarRecibido();

        act.Should().Throw<TransicionPedidoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPedidoEnum.Cancelado);
    }

    // ───────────────────── Cancelación ─────────────────────

    [Fact]
    public void Cancelar_PedidoEmitido_PasaACanceladoConMotivoNormalizado()
    {
        var pedido = Emitido();

        pedido.Cancelar("  sin   stock ");

        pedido.Estado.Should().Be(EstadoPedidoEnum.Cancelado);
        pedido.MotivoCancelacion.Should().Be("sin stock");
        pedido.FechaCierre.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancelar_SinMotivo_LanzaExcepcion(string motivo)
    {
        var pedido = Emitido();

        var act = () => pedido.Cancelar(motivo);

        act.Should().Throw<DomainException>().WithMessage("*motivo*");
        pedido.Estado.Should().Be(EstadoPedidoEnum.Emitido);
    }

    [Fact]
    public void Cancelar_PedidoRecibido_LanzaTransicionInvalida()
    {
        var pedido = Emitido();
        pedido.MarcarRecibido();

        var act = () => pedido.Cancelar("error");

        act.Should().Throw<TransicionPedidoInvalidaException>()
            .Which.EstadoActual.Should().Be(EstadoPedidoEnum.Recibido);
    }

    // ───────────────────── Total ─────────────────────

    [Fact]
    public void TotalEstimado_VariosRenglones_SumaRedondeada()
    {
        // 3 × 10.335 = 31.005 y 2 × 0.50 = 1.00 → suma 32.005, un punto medio exacto.
        // Se redondea una sola vez al final, alejándose del cero: 32.01 (el redondeo bancario daría 32.00).
        var pedido = Borrador();
        pedido.AgregarRenglon(10, 100, "LAP-01", "Lápiz negro", 3, 10.335m);
        pedido.AgregarRenglon(11, 101, "GOM-01", "Goma", 2, 0.50m);

        pedido.TotalEstimado.Should().Be(32.01m);
    }
}
