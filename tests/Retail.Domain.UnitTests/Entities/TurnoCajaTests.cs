using FluentAssertions;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Entities;

public class TurnoCajaTests
{
    [Fact]
    public void Abrir_TurnoNuevo_CantidadVentasEnCero()
    {
        // Act
        var turno = TurnoCaja.Abrir(1, 1, 1000m);

        // Assert
        turno.CantidadVentas.Should().Be(0);
    }

    [Fact]
    public void ImputarVenta_SoloCuentaCorriente_IncrementaCantidadVentas()
    {
        // Arrange: una venta 100 % a cuenta corriente no mueve ni efectivo ni dinero electrónico (H18).
        var turno = TurnoCaja.Abrir(1, 1, 1000m);

        // Act
        turno.ImputarVenta(0m, 0m);

        // Assert: el contador es lo único que cambia, y alcanza para que EF Core emita el UPDATE.
        turno.CantidadVentas.Should().Be(1);
        turno.SaldoTeoricoEfectivo.Should().Be(1000m);
        turno.TotalVentasEfectivo.Should().Be(0m);
        turno.TotalVentasElectronicas.Should().Be(0m);
    }

    [Fact]
    public void ImputarVenta_TotalCero_IncrementaCantidadVentas()
    {
        // Arrange
        var turno = TurnoCaja.Abrir(1, 1, 0m);
        turno.ImputarVenta(0m, 0m);

        // Act
        turno.ImputarVenta(0m, 0m);

        // Assert
        turno.CantidadVentas.Should().Be(2);
    }

    [Fact]
    public void ImputarVenta_EnEfectivo_IncrementaCantidadVentasYSaldoTeorico()
    {
        // Arrange
        var turno = TurnoCaja.Abrir(1, 1, 1000m);

        // Act
        turno.ImputarVenta(250m, 100m);

        // Assert
        turno.CantidadVentas.Should().Be(1);
        turno.TotalVentasEfectivo.Should().Be(250m);
        turno.TotalVentasElectronicas.Should().Be(100m);
        turno.SaldoTeoricoEfectivo.Should().Be(1250m);
    }

    [Fact]
    public void ImputarVenta_TurnoCerrado_LanzaTurnoYaCerradoYNoIncrementa()
    {
        // Arrange
        var turno = TurnoCaja.Abrir(1, 1, 1000m);
        turno.CerrarConArqueoCiego(1000m, 0m);

        // Act
        var accion = () => turno.ImputarVenta(0m, 0m);

        // Assert
        accion.Should().Throw<TurnoYaCerradoException>();
        turno.CantidadVentas.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Abrir_TerminalInvalida_LanzaDomainException(int idTerminal)
    {
        // Act
        var accion = () => TurnoCaja.Abrir(1, idTerminal, 1000m);

        // Assert
        accion.Should().Throw<DomainException>().WithMessage("*terminal*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Abrir_UsuarioInvalido_LanzaDomainException(int idUsuario)
    {
        // Act
        var accion = () => TurnoCaja.Abrir(idUsuario, 1, 1000m);

        // Assert
        accion.Should().Throw<DomainException>().WithMessage("*usuario*");
    }

    [Fact]
    public void Abrir_DatosValidos_GuardaLaTerminal()
    {
        // Act
        var turno = TurnoCaja.Abrir(4, 7, 1000m);

        // Assert
        turno.IdUsuario.Should().Be(4);
        turno.IdTerminal.Should().Be(7);
    }

    [Fact]
    public void EsDe_MismoUsuario_DevuelveVerdadero()
    {
        TurnoCaja.Abrir(4, 1, 0m).EsDe(4).Should().BeTrue();
    }

    [Fact]
    public void EsDe_OtroUsuario_DevuelveFalso()
    {
        TurnoCaja.Abrir(4, 1, 0m).EsDe(5).Should().BeFalse();
    }

    [Fact]
    public void ExigirSinCambiosDesde_ValoresIguales_NoLanza()
    {
        // Arrange
        var turno = TurnoCaja.Abrir(1, 1, 1000m);
        turno.ImputarVenta(250m, 100m);

        // Act
        var accion = () => turno.ExigirSinCambiosDesde(1250m, 100m, 1);

        // Assert
        accion.Should().NotThrow();
    }

    [Theory]
    [InlineData(1251, 100, 1)]
    [InlineData(1250, 101, 1)]
    [InlineData(1250, 100, 2)]
    public void ExigirSinCambiosDesde_UnValorDistinto_LanzaTurnoModificado(int saldoTeorico, int totalElectronico, int cantidadVentas)
    {
        // Arrange
        var turno = TurnoCaja.Abrir(1, 1, 1000m);
        turno.ImputarVenta(250m, 100m);

        // Act
        var accion = () => turno.ExigirSinCambiosDesde(saldoTeorico, totalElectronico, cantidadVentas);

        // Assert
        accion.Should().Throw<TurnoModificadoException>()
            .WithMessage("*Vuelva a abrir el cierre*");
    }
}
