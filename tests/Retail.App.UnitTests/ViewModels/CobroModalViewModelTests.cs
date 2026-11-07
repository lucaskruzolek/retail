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
    public void Constructor_SinPagos_PreseleccionaEfectivoConElTotalCargado()
    {
        var viewModel = new CobroModalViewModel(15000m, null);

        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.Efectivo);
        viewModel.Monto.Should().Be(15000m);
        viewModel.SaldoPendiente.Should().Be(15000m);
        viewModel.TotalImputado.Should().Be(0m);
        viewModel.CubreLoQueFalta.Should().BeTrue();
        viewModel.TextoAccionPrincipal.Should().Be("Cobrar");
    }

    [Fact]
    public void SeleccionarMedioSiguiente_ConservaElMonto()
    {
        var viewModel = new CobroModalViewModel(15000m, null);

        viewModel.SeleccionarMedioSiguiente();

        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.TarjetaDebito);
        viewModel.Monto.Should().Be(15000m);
        viewModel.EtiquetaMonto.Should().Be("Monto");
    }

    [Fact]
    public void SeleccionarMedioAnterior_EnElPrimero_NoDaLaVuelta()
    {
        var viewModel = new CobroModalViewModel(15000m, null);

        viewModel.SeleccionarMedioAnterior();

        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.Efectivo);
    }

    [Fact]
    public void SeleccionarMedioSiguiente_SinCuentaCorrienteHabilitada_LaSaltea()
    {
        var viewModel = new CobroModalViewModel(5000m, CrearClienteSinCuentaCorriente())
        {
            MedioPagoSeleccionado = MedioPagoEnum.TransferenciaQr
        };

        viewModel.SeleccionarMedioSiguiente();

        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.TransferenciaQr);
        viewModel.EstaHabilitado(MedioPagoEnum.CuentaCorriente).Should().BeFalse();
    }

    [Fact]
    public void SeleccionarMedioSiguiente_ConCuentaCorrienteHabilitada_LaSelecciona()
    {
        var viewModel = new CobroModalViewModel(5000m, CrearClienteConCuentaCorriente())
        {
            MedioPagoSeleccionado = MedioPagoEnum.TransferenciaQr
        };

        viewModel.SeleccionarMedioSiguiente();

        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.CuentaCorriente);
    }

    [Fact]
    public void Efectivo_SumarBilletes_DebeAcumularMontoRecibido()
    {
        var viewModel = new CobroModalViewModel(10000m, null);
        viewModel.Monto = 0m;

        viewModel.SumarMil();
        viewModel.SumarDosMil();
        viewModel.SumarCincoMil();
        viewModel.SumarDiezMil();
        viewModel.SumarVeinteMil();

        viewModel.Monto.Should().Be(38000m);
    }

    [Fact]
    public void Efectivo_PrimerBillete_ReemplazaElMontoPrecargado()
    {
        // El modal precarga lo que falta cobrar como sugerencia (D-16)
        var viewModel = new CobroModalViewModel(3500m, null);

        viewModel.SumarCincoMil();

        viewModel.Monto.Should().Be(5000m);
        viewModel.Vuelto.Should().Be(1500m);
    }

    [Fact]
    public void Efectivo_BilletesSiguientes_SumanAlPrimero()
    {
        var viewModel = new CobroModalViewModel(3500m, null);

        // El cliente entrega dos billetes de $2.000
        viewModel.SumarDosMil();
        viewModel.SumarDosMil();

        viewModel.Monto.Should().Be(4000m);
        viewModel.Vuelto.Should().Be(500m);
    }

    [Fact]
    public void Efectivo_Exacto_ReiniciaElConteoDeBilletes()
    {
        var viewModel = new CobroModalViewModel(3500m, null);
        viewModel.SumarCincoMil();

        viewModel.EstablecerPagoExacto();
        var montoTrasExacto = viewModel.Monto;
        viewModel.SumarMil();

        montoTrasExacto.Should().Be(3500m);
        viewModel.Monto.Should().Be(1000m);
    }

    [Fact]
    public void Efectivo_MontoTipeadoPorElCajero_LosBilletesLeSuman()
    {
        var viewModel = new CobroModalViewModel(3500m, null);
        viewModel.Monto = 3000m;

        viewModel.SumarMil();

        viewModel.Monto.Should().Be(4000m);
    }

    [Fact]
    public void Efectivo_MontoMayorALoQueFalta_LaDiferenciaEsVuelto()
    {
        var viewModel = new CobroModalViewModel(7500m, null);

        viewModel.Monto = 10000m;

        viewModel.Vuelto.Should().Be(2500m);
        viewModel.MontoAImputar.Should().Be(7500m);
        viewModel.TieneVuelto.Should().BeTrue();
    }

    [Fact]
    public void ConfirmarPagoActual_MontoIgualALoQueFalta_ImputaYConfirmaLaOperacion()
    {
        var viewModel = new CobroModalViewModel(5000m, null)
        {
            MedioPagoSeleccionado = MedioPagoEnum.TarjetaDebito
        };

        viewModel.ConfirmarPagoActual();

        viewModel.OperacionConfirmada.Should().BeTrue();
        viewModel.PagosImputados.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { MedioPago = MedioPagoEnum.TarjetaDebito, Monto = 5000m });
        viewModel.SaldoPendiente.Should().Be(0m);
    }

    [Fact]
    public void ConfirmarPagoActual_EfectivoMayorALoQueFalta_ImputaLoQueFaltaConVueltoYConfirma()
    {
        var viewModel = new CobroModalViewModel(18650m, null)
        {
            Monto = 20000m
        };

        viewModel.ConfirmarPagoActual();

        viewModel.OperacionConfirmada.Should().BeTrue();
        var pago = viewModel.PagosImputados.Should().ContainSingle().Subject;
        pago.Monto.Should().Be(18650m);
        pago.MontoRecibido.Should().Be(20000m);
        pago.Vuelto.Should().Be(1350m);
    }

    [Fact]
    public void ConfirmarPagoActual_MontoMenor_ImputaParcialYPreparaOtroMedioConElRestante()
    {
        var viewModel = new CobroModalViewModel(18650m, null)
        {
            Monto = 10000m
        };

        viewModel.ConfirmarPagoActual();

        viewModel.OperacionConfirmada.Should().BeFalse();
        viewModel.PagosImputados.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { MedioPago = MedioPagoEnum.Efectivo, Monto = 10000m, Vuelto = 0m });
        viewModel.SaldoPendiente.Should().Be(8650m);
        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.TarjetaDebito);
        viewModel.Monto.Should().Be(8650m);
        viewModel.TextoAccionPrincipal.Should().Be("Cobrar");
    }

    [Fact]
    public void ConfirmarPagoActual_SegundoPagoQueCompleta_ConfirmaLaOperacion()
    {
        var viewModel = new CobroModalViewModel(18650m, null)
        {
            Monto = 10000m
        };
        viewModel.ConfirmarPagoActual();

        viewModel.ConfirmarPagoActual();

        viewModel.OperacionConfirmada.Should().BeTrue();
        viewModel.PagosImputados.Should().HaveCount(2);
        viewModel.TotalImputado.Should().Be(18650m);
    }

    [Fact]
    public void ConfirmarPagoActual_TarjetaMayorALoQueFalta_MuestraErrorSinImputar()
    {
        var viewModel = new CobroModalViewModel(5000m, null)
        {
            MedioPagoSeleccionado = MedioPagoEnum.TarjetaCredito,
            Monto = 6000m
        };

        viewModel.ConfirmarPagoActual();

        viewModel.MensajeError.Should().Contain("solo el efectivo da vuelto");
        viewModel.PagosImputados.Should().BeEmpty();
        viewModel.OperacionConfirmada.Should().BeFalse();
    }

    [Fact]
    public void ConfirmarPagoActual_MontoEnCero_MuestraErrorSinImputar()
    {
        var viewModel = new CobroModalViewModel(5000m, null)
        {
            Monto = 0m
        };

        viewModel.ConfirmarPagoActual();

        viewModel.TieneError.Should().BeTrue();
        viewModel.PagosImputados.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmarPagoActual_CuentaCorrienteSinHabilitacion_MuestraError()
    {
        var viewModel = new CobroModalViewModel(5000m, CrearClienteSinCuentaCorriente())
        {
            MedioPagoSeleccionado = MedioPagoEnum.CuentaCorriente
        };

        viewModel.ConfirmarPagoActual();

        viewModel.MensajeError.Should().Contain("cuenta corriente habilitada");
        viewModel.PagosImputados.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmarPagoActual_CuentaCorrienteSuperaElCreditoDisponible_MuestraError()
    {
        // Límite 50.000, debe 45.000 -> disponible 5.000
        var viewModel = new CobroModalViewModel(8000m, CrearClienteConCuentaCorriente(50000m, 45000m))
        {
            MedioPagoSeleccionado = MedioPagoEnum.CuentaCorriente
        };

        viewModel.ConfirmarPagoActual();

        viewModel.MensajeError.Should().Contain("supera el crédito disponible");
        viewModel.PagosImputados.Should().BeEmpty();
    }

    [Fact]
    public void Efectivo_TrasUnPagoParcial_ElPrimerBilleteVuelveAReemplazar()
    {
        // Se cobran $4.000 con débito de un total de $10.000: queda sugerido el resto, $6.000, en efectivo
        var viewModel = new CobroModalViewModel(10000m, null)
        {
            MedioPagoSeleccionado = MedioPagoEnum.TarjetaDebito,
            Monto = 4000m
        };
        viewModel.ConfirmarPagoActual();

        var sugerido = viewModel.Monto;
        viewModel.SumarDiezMil();

        viewModel.MedioPagoSeleccionado.Should().Be(MedioPagoEnum.Efectivo);
        sugerido.Should().Be(6000m);
        viewModel.Monto.Should().Be(10000m);
        viewModel.Vuelto.Should().Be(4000m);
    }

    [Fact]
    public void RemoverPago_DespuesDeUnParcial_RecalculaElMontoPendiente()
    {
        var viewModel = new CobroModalViewModel(10000m, null)
        {
            Monto = 4000m
        };
        viewModel.ConfirmarPagoActual();

        viewModel.RemoverPago(viewModel.PagosImputados[0]);

        viewModel.PagosImputados.Should().BeEmpty();
        viewModel.SaldoPendiente.Should().Be(10000m);
        viewModel.Monto.Should().Be(10000m);
    }
}
