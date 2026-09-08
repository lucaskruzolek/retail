using FluentAssertions;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.Domain.UnitTests.Exceptions;

public class DomainExceptionsTests
{
    [Fact]
    public void StockInsuficienteException_DebeRetenerPropiedadesDelError()
    {
        var ex = new StockInsuficienteException(articuloId: 42, stockActual: 5, cantidadSolicitada: 10);

        ex.ArticuloId.Should().Be(42);
        ex.StockActual.Should().Be(5);
        ex.CantidadSolicitada.Should().Be(10);
        ex.Message.Should().Contain("42");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void CajaCerradaException_DebeHeredarDeDomainExceptionConMensajePorDefecto()
    {
        var ex = new CajaCerradaException();

        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void PresupuestoVencidoException_DebeRetenerIdYFechaVencimiento()
    {
        var fechaVencimiento = DateTime.UtcNow.AddDays(-1);

        var ex = new PresupuestoVencidoException(presupuestoId: 101, fechaVencimiento: fechaVencimiento);

        ex.PresupuestoId.Should().Be(101);
        ex.FechaVencimiento.Should().Be(fechaVencimiento);
        ex.Message.Should().Contain("101");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void LimiteCreditoExcedidoException_DebeRetenerDatosDeCuentaCorriente()
    {
        var ex = new LimiteCreditoExcedidoException(
            clienteId: 9,
            saldoActual: 8000m,
            limiteCredito: 10000m,
            montoSolicitado: 3000m);

        ex.ClienteId.Should().Be(9);
        ex.SaldoActual.Should().Be(8000m);
        ex.LimiteCredito.Should().Be(10000m);
        ex.MontoSolicitado.Should().Be(3000m);
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void TurnoYaAbiertoException_DebeRetenerIdTurnoActivo()
    {
        var ex = new TurnoYaAbiertoException(turnoActivoId: 15);

        ex.TurnoActivoId.Should().Be(15);
        ex.Message.Should().Contain("15");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void SaldoCajaInsuficienteException_DebeRetenerMontosYTurno()
    {
        var ex = new SaldoCajaInsuficienteException(turnoId: 3, saldoDisponible: 5000m, montoRetiro: 12000m);

        ex.TurnoId.Should().Be(3);
        ex.SaldoDisponible.Should().Be(5000m);
        ex.MontoRetiro.Should().Be(12000m);
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void TurnoYaCerradoException_DebeRetenerIdTurno()
    {
        var ex = new TurnoYaCerradoException(turnoId: 8);

        ex.TurnoId.Should().Be(8);
        ex.Message.Should().Contain("8");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void CuentaCorrienteNoHabilitadaException_DebeRetenerIdCliente()
    {
        var ex = new CuentaCorrienteNoHabilitadaException(clienteId: 77);

        ex.ClienteId.Should().Be(77);
        ex.Message.Should().Contain("77");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void CobranzaExcedeDeudaException_DebeRetenerDatosDeuda()
    {
        var ex = new CobranzaExcedeDeudaException(clienteId: 12, saldoDeudor: 4000m, montoCobranza: 6000m);

        ex.ClienteId.Should().Be(12);
        ex.SaldoDeudor.Should().Be(4000m);
        ex.MontoCobranza.Should().Be(6000m);
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void PresupuestoYaConvertidoException_DebeRetenerIdPresupuesto()
    {
        var ex = new PresupuestoYaConvertidoException(presupuestoId: 205);

        ex.PresupuestoId.Should().Be(205);
        ex.Message.Should().Contain("205");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void MontoPagoInsuficienteException_DebeCalcularFaltanteCorrectamente()
    {
        var ex = new MontoPagoInsuficienteException(totalVenta: 10000m, totalPagado: 7500m);

        ex.TotalVenta.Should().Be(10000m);
        ex.TotalPagado.Should().Be(7500m);
        ex.Faltante.Should().Be(2500m);
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void VentaVaciaException_DebeTenerMensajePorDefecto()
    {
        var ex = new VentaVaciaException();

        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void UltimoGerenteException_DebeRetenerIdUsuario()
    {
        var ex = new UltimoGerenteException(usuarioId: 1);

        ex.UsuarioId.Should().Be(1);
        ex.Message.Should().Contain("1");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void CredencialesInvalidasException_DebeTenerMensajePorDefecto()
    {
        var ex = new CredencialesInvalidasException();

        ex.Message.Should().NotBeNullOrWhiteSpace();
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void UsuarioInactivoException_DebeRetenerNombreUsuario()
    {
        var ex = new UsuarioInactivoException(nombreUsuario: "juan_cajero");

        ex.NombreUsuario.Should().Be("juan_cajero");
        ex.Message.Should().Contain("juan_cajero");
        ex.Should().BeAssignableTo<DomainException>();
    }
}
