using FluentAssertions;
using Retail.App.ViewModels.Ventas;
using Retail.Application.DTOs.Clientes;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class CobroModalViewModelTests
{
    private static ClienteDto CrearClienteConCuentaCorriente(decimal limiteCredito = 50000m, decimal saldoDeudor = 10000m)
    {
        return new ClienteDto
        {
            IdCliente = 1,
            RazonSocialONombre = "Librería Central",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30123456789",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = limiteCredito,
            SaldoCuentaCorriente = saldoDeudor
        };
    }

    private static ClienteDto CrearClienteSinCuentaCorriente()
    {
        return new ClienteDto
        {
            IdCliente = 2,
            RazonSocialONombre = "Juan Pérez",
            TipoDocumento = TipoDocumentoEnum.Dni,
            NumeroDocumento = "38123456",
            CondicionIva = CondicionIvaEnum.ConsumidorFinal,
            TieneCuentaCorriente = false,
            LimiteCredito = 0m,
            SaldoCuentaCorriente = 0m
        };
    }

    [Fact]
    public void Inicializacion_DebeFijarTotalYSaldoPendiente()
    {
        // Arrange & Act
        var viewModel = new CobroModalViewModel(15000m, null);

        // Assert
        viewModel.TotalVenta.Should().Be(15000m);
        viewModel.SaldoPendiente.Should().Be(15000m);
        viewModel.TotalImputado.Should().Be(0m);
        viewModel.MontoImputar.Should().Be(15000m);
        viewModel.EfectivoRecibido.Should().Be(15000m);
        viewModel.TieneSaldoPendiente.Should().BeTrue();
        viewModel.PuedeConfirmar.Should().BeFalse();
    }

    [Fact]
    public void Efectivo_SumarBilletes_DebeAcumularMontoRecibido()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(10000m, null);
        viewModel.EfectivoRecibido = 0m;

        // Act
        viewModel.SumarMil();
        viewModel.SumarDosMil();
        viewModel.SumarCincoMil();
        viewModel.SumarDiezMil();
        viewModel.SumarVeinteMil();

        // Assert
        viewModel.EfectivoRecibido.Should().Be(38000m);
    }

    [Fact]
    public void Efectivo_PrimerBillete_ReemplazaElMontoExactoPrecargado()
    {
        // Arrange: el modal precarga el monto exacto como sugerencia
        var viewModel = new CobroModalViewModel(3500m, null);

        // Act
        viewModel.SumarCincoMil();

        // Assert
        viewModel.EfectivoRecibido.Should().Be(5000m);
        viewModel.Vuelto.Should().Be(1500m);
    }

    [Fact]
    public void Efectivo_BilletesSiguientes_SumanAlPrimero()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(3500m, null);

        // Act: el cliente entrega dos billetes de $2.000
        viewModel.SumarDosMil();
        viewModel.SumarDosMil();

        // Assert
        viewModel.EfectivoRecibido.Should().Be(4000m);
        viewModel.Vuelto.Should().Be(500m);
    }

    [Fact]
    public void Efectivo_Exacto_ReiniciaElConteoDeBilletes()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(3500m, null);
        viewModel.SumarCincoMil();

        // Act
        viewModel.EstablecerPagoExacto();
        var recibidoTrasExacto = viewModel.EfectivoRecibido;
        viewModel.SumarMil();

        // Assert
        recibidoTrasExacto.Should().Be(3500m);
        viewModel.EfectivoRecibido.Should().Be(1000m);
    }

    [Fact]
    public void Efectivo_MontoTipeadoPorElCajero_LosBilletesLeSuman()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(3500m, null);
        viewModel.EfectivoRecibido = 3000m;

        // Act
        viewModel.SumarMil();

        // Assert
        viewModel.EfectivoRecibido.Should().Be(4000m);
    }

    [Fact]
    public void Efectivo_TrasImputarUnPagoParcial_ElPrimerBilleteVuelveAReemplazar()
    {
        // Arrange: se imputan $4.000 de un total de $10.000 y queda sugerido el saldo de $6.000
        var viewModel = new CobroModalViewModel(10000m, null);
        viewModel.MontoImputar = 4000m;
        viewModel.SumarCincoMil();
        viewModel.ImputarPago();

        // Act
        var sugerido = viewModel.EfectivoRecibido;
        viewModel.SumarDiezMil();

        // Assert
        sugerido.Should().Be(6000m);
        viewModel.EfectivoRecibido.Should().Be(10000m);
        viewModel.Vuelto.Should().Be(4000m);
    }

    [Fact]
    public void Efectivo_CalculoVuelto_DebeSerDiferenciaEntreRecibidoYTotal()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(7500m, null);

        // Act
        viewModel.EfectivoRecibido = 10000m;

        // Assert
        viewModel.Vuelto.Should().Be(2500m);
    }

    [Fact]
    public void CuentaCorriente_SinHabilitacion_DebeEmitirErrorAlImputar()
    {
        // Arrange
        var clienteSinCtaCte = CrearClienteSinCuentaCorriente();
        var viewModel = new CobroModalViewModel(5000m, clienteSinCtaCte)
        {
            MedioPagoSeleccionado = MedioPagoEnum.CuentaCorriente
        };

        // Act
        viewModel.ImputarPago();

        // Assert
        viewModel.MensajeError.Should().Contain("cuenta corriente habilitada");
        viewModel.PagosImputados.Should().BeEmpty();
    }

    [Fact]
    public void CuentaCorriente_SuperaCreditoDisponible_DebeEmitirError()
    {
        // Arrange (Límite 50.000, debe 45.000 -> Disponible 5.000)
        var cliente = CrearClienteConCuentaCorriente(50000m, 45000m);
        var viewModel = new CobroModalViewModel(8000m, cliente)
        {
            MedioPagoSeleccionado = MedioPagoEnum.CuentaCorriente,
            MontoImputar = 8000m
        };

        // Act
        viewModel.ImputarPago();

        // Assert
        viewModel.MensajeError.Should().Contain("supera el crédito disponible");
        viewModel.PagosImputados.Should().BeEmpty();
    }

    [Fact]
    public void ImputarPago_PagoParcial_DebeReducirSaldoPendiente()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(10000m, null)
        {
            MedioPagoSeleccionado = MedioPagoEnum.Efectivo,
            MontoImputar = 4000m,
            EfectivoRecibido = 4000m
        };

        // Act
        viewModel.ImputarPago();

        // Assert
        viewModel.PagosImputados.Should().HaveCount(1);
        viewModel.TotalImputado.Should().Be(4000m);
        viewModel.SaldoPendiente.Should().Be(6000m);
        viewModel.MontoImputar.Should().Be(6000m);
        viewModel.PuedeConfirmar.Should().BeFalse();
    }

    [Fact]
    public void ConfirmarCobro_ConSaldoPendiente_NoDebeConfirmar()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(5000m, null);

        // Act
        viewModel.ConfirmarCobro();

        // Assert
        viewModel.OperacionConfirmada.Should().BeFalse();
        viewModel.MensajeError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ConfirmarCobro_ConSaldoCubierto_DebeConfirmarExitosamente()
    {
        // Arrange
        var viewModel = new CobroModalViewModel(5000m, null)
        {
            MedioPagoSeleccionado = MedioPagoEnum.Efectivo,
            MontoImputar = 5000m,
            EfectivoRecibido = 5000m
        };

        viewModel.ImputarPago();

        // Act
        viewModel.ConfirmarCobro();

        // Assert
        viewModel.OperacionConfirmada.Should().BeTrue();
        viewModel.SaldoPendiente.Should().Be(0m);
        viewModel.PuedeConfirmar.Should().BeTrue();
    }
}
