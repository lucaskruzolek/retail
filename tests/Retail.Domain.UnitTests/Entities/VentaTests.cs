using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class VentaTests
{
    private const int IdTurno = 1;
    private const int IdUsuario = 2;
    private const int IdCliente = 3;

    [Fact]
    public void Registrar_ConsumidorFinal_CreaVentaVaciaSinCliente()
    {
        // Act
        var venta = Venta.Registrar(IdTurno, IdUsuario, idCliente: null);

        // Assert
        venta.IdTurno.Should().Be(IdTurno);
        venta.IdUsuario.Should().Be(IdUsuario);
        venta.IdCliente.Should().BeNull();
        venta.Detalles.Should().BeEmpty();
        venta.Pagos.Should().BeEmpty();
        venta.Total.Should().Be(0m);
        venta.EstadoFiscal.Should().Be(EstadoFiscalEnum.NoAplica);
    }

    [Theory]
    [InlineData(0, IdUsuario, null)]
    [InlineData(IdTurno, 0, null)]
    [InlineData(IdTurno, IdUsuario, 0)]
    public void Registrar_IdentificadoresInvalidos_LanzaArgumentOutOfRangeException(int idTurno, int idUsuario, int? idCliente)
    {
        // Act
        var act = () => Venta.Registrar(idTurno, idUsuario, idCliente);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AgregarItem_VariosArticulos_CalculaSubtotalYTotal()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);

        // Act
        venta.AgregarItem(idArticulo: 10, cantidad: 3, precioUnitario: 150.50m);
        venta.AgregarItem(idArticulo: 11, cantidad: 1, precioUnitario: 99.99m);

        // Assert
        venta.Detalles.Should().HaveCount(2);
        venta.Detalles.Single(d => d.IdArticulo == 10).SubtotalItem.Should().Be(451.50m);
        venta.Subtotal.Should().Be(551.49m);
        venta.Total.Should().Be(551.49m);
    }

    [Fact]
    public void AgregarItem_ArticuloRepetidoConMismoPrecio_SumaCantidadEnLaMismaLinea()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 2, 100m);

        // Act
        venta.AgregarItem(10, 3, 100m);

        // Assert
        venta.Detalles.Should().ContainSingle();
        venta.Detalles.Single().Cantidad.Should().Be(5);
        venta.Total.Should().Be(500m);
    }

    [Fact]
    public void AgregarItem_ArticuloRepetidoConOtroPrecio_LanzaDomainExceptionSinModificarLaLinea()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 2, 100m);

        // Act
        var act = () => venta.AgregarItem(10, 1, 120m);

        // Assert
        act.Should().Throw<DomainException>();
        venta.Detalles.Single().Cantidad.Should().Be(2);
        venta.Total.Should().Be(200m);
    }

    [Theory]
    [InlineData(0, 1, 100)]
    [InlineData(10, 0, 100)]
    [InlineData(10, 1, -1)]
    public void AgregarItem_ValoresInvalidos_LanzaArgumentOutOfRangeException(int idArticulo, int cantidad, decimal precioUnitario)
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);

        // Act
        var act = () => venta.AgregarItem(idArticulo, cantidad, precioUnitario);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        venta.Detalles.Should().BeEmpty();
    }

    [Fact]
    public void AplicarDescuento_MenorAlSubtotal_ReduceElTotal()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 2, 500m);

        // Act
        venta.AplicarDescuento(150m);

        // Assert
        venta.Subtotal.Should().Be(1000m);
        venta.Descuento.Should().Be(150m);
        venta.Total.Should().Be(850m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000.01)]
    public void AplicarDescuento_NegativoOMayorAlSubtotal_LanzaDomainExceptionSinModificarElTotal(decimal descuento)
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 2, 500m);

        // Act
        var act = () => venta.AplicarDescuento(descuento);

        // Assert
        act.Should().Throw<DomainException>();
        venta.Descuento.Should().Be(0m);
        venta.Total.Should().Be(1000m);
    }

    [Fact]
    public void ImputarPago_MediosMixtos_ClasificaEfectivoElectronicoYCuentaCorriente()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, IdCliente);
        venta.AgregarItem(10, 1, 1000m);

        // Act
        venta.ImputarPago(MedioPagoEnum.Efectivo, 300m);
        venta.ImputarPago(MedioPagoEnum.TarjetaDebito, 200m, "  Lote 42  ");
        venta.ImputarPago(MedioPagoEnum.TransferenciaQr, 100m);
        venta.ImputarPago(MedioPagoEnum.CuentaCorriente, 400m);

        // Assert
        venta.TotalPagado.Should().Be(1000m);
        venta.TotalEfectivo.Should().Be(300m);
        venta.TotalElectronico.Should().Be(300m);
        venta.TotalCuentaCorriente.Should().Be(400m);
        venta.Pagos.Single(p => p.MedioPago == MedioPagoEnum.TarjetaDebito).ReferenciaPago.Should().Be("Lote 42");
    }

    [Fact]
    public void ImputarPago_CuentaCorrienteSinCliente_LanzaCuentaCorrienteNoHabilitadaException()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, idCliente: null);
        venta.AgregarItem(10, 1, 1000m);

        // Act
        var act = () => venta.ImputarPago(MedioPagoEnum.CuentaCorriente, 1000m);

        // Assert
        act.Should().Throw<CuentaCorrienteNoHabilitadaException>();
        venta.Pagos.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ImputarPago_MontoNoPositivo_LanzaArgumentOutOfRangeException(decimal monto)
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);

        // Act
        var act = () => venta.ImputarPago(MedioPagoEnum.Efectivo, monto);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ImputarPago_MedioDePagoInexistente_LanzaArgumentOutOfRangeException()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);

        // Act
        var act = () => venta.ImputarPago((MedioPagoEnum)99, 100m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidarCierre_PagosIgualesAlTotal_NoLanzaExcepcion()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 2, 500m);
        venta.AplicarDescuento(100m);
        venta.ImputarPago(MedioPagoEnum.Efectivo, 900m);

        // Act
        var act = () => venta.ValidarCierre();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidarCierre_SinItems_LanzaVentaVaciaException()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);

        // Act
        var act = () => venta.ValidarCierre();

        // Assert
        act.Should().Throw<VentaVaciaException>();
    }

    [Fact]
    public void ValidarCierre_PagosMenoresAlTotal_LanzaMontoPagoInsuficienteExceptionConElFaltante()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 1, 1000m);
        venta.ImputarPago(MedioPagoEnum.Efectivo, 700m);

        // Act
        var act = () => venta.ValidarCierre();

        // Assert
        act.Should().Throw<MontoPagoInsuficienteException>()
            .Which.Faltante.Should().Be(300m);
    }

    [Fact]
    public void ValidarCierre_PagosMayoresAlTotal_LanzaDomainException()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 1, 1000m);
        venta.ImputarPago(MedioPagoEnum.Efectivo, 1200m);

        // Act
        var act = () => venta.ValidarCierre();

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ValidarCierre_DescuentoTotalSinPagos_NoLanzaExcepcion()
    {
        // Arrange
        var venta = Venta.Registrar(IdTurno, IdUsuario, null);
        venta.AgregarItem(10, 1, 1000m);
        venta.AplicarDescuento(1000m);

        // Act
        var act = () => venta.ValidarCierre();

        // Assert
        venta.Total.Should().Be(0m);
        act.Should().NotThrow();
    }
}
