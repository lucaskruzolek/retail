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
        var turno = TurnoCaja.Abrir(1, 1000m);

        // Assert
        turno.CantidadVentas.Should().Be(0);
    }

    [Fact]
    public void ImputarVenta_SoloCuentaCorriente_IncrementaCantidadVentas()
    {
        // Arrange: una venta 100 % a cuenta corriente no mueve ni efectivo ni dinero electrónico (H18).
        var turno = TurnoCaja.Abrir(1, 1000m);

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
        var turno = TurnoCaja.Abrir(1, 0m);
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
        var turno = TurnoCaja.Abrir(1, 1000m);

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
        var turno = TurnoCaja.Abrir(1, 1000m);
        turno.CerrarConArqueoCiego(1000m, 0m);

        // Act
        var accion = () => turno.ImputarVenta(0m, 0m);

        // Assert
        accion.Should().Throw<TurnoYaCerradoException>();
        turno.CantidadVentas.Should().Be(0);
    }
}
