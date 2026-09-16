using FluentAssertions;
using NSubstitute;
using Retail.App.ViewModels.Clientes;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class CobranzaModalViewModelTests
{
    private readonly IClienteService _clienteService;
    private readonly ICajaService _cajaService;
    private readonly ClienteDto _clienteEjemplo;

    public CobranzaModalViewModelTests()
    {
        _clienteService = Substitute.For<IClienteService>();
        _cajaService = Substitute.For<ICajaService>();

        _clienteEjemplo = new ClienteDto
        {
            IdCliente = 7,
            RazonSocialONombre = "Comercial Belgrano",
            TipoDocumento = TipoDocumentoEnum.Cuit,
            NumeroDocumento = "30-77777777-7",
            CondicionIva = CondicionIvaEnum.ResponsableInscripto,
            TieneCuentaCorriente = true,
            LimiteCredito = 50000m,
            SaldoCuentaCorriente = 12000m
        };
    }

    [Fact]
    public void Constructor_InicializaValoresConDeudaDelCliente()
    {
        // Act
        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo);

        // Assert
        vm.Cliente.Should().Be(_clienteEjemplo);
        vm.SaldoActual.Should().Be(12000m);
        vm.MontoCobro.Should().Be(12000m);
        vm.EfectivoRecibido.Should().Be(12000m);
        vm.Vuelto.Should().Be(0m);
        vm.MedioPago.Should().Be(MedioPagoEnum.Efectivo);
        vm.EsEfectivo.Should().BeTrue();
        vm.TieneError.Should().BeFalse();
        vm.ImprimirRecibo.Should().BeTrue();
    }

    [Fact]
    public void PagarTotalidadCommand_AsignaSaldoActualAMontoCobro()
    {
        // Arrange
        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 4000m
        };

        // Act
        vm.PagarTotalidadCommand.Execute(null);

        // Assert
        vm.MontoCobro.Should().Be(12000m);
    }

    [Fact]
    public void RecalcularVuelto_CuandoEsEfectivoYRecibidoSuperaMonto_CalculaVueltoPositivo()
    {
        // Arrange
        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 8000m,
            EfectivoRecibido = 10000m
        };

        // Assert
        vm.Vuelto.Should().Be(2000m);
    }

    [Fact]
    public async Task CobrarAsync_MontoCeroONegativo_MuestraErrorYNoLlamaServicio()
    {
        // Arrange
        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 0m
        };

        // Act
        await vm.CobrarCommand.ExecuteAsync(null);

        // Assert
        vm.TieneError.Should().BeTrue();
        vm.MensajeError.Should().Contain("mayor a $ 0,00");
        await _clienteService.DidNotReceive().RegistrarCobranzaAsync(Arg.Any<RegistrarCobranzaDto>());
    }

    [Fact]
    public async Task CobrarAsync_MontoMayorASaldo_MuestraError()
    {
        // Arrange
        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 15000m
        };

        // Act
        await vm.CobrarCommand.ExecuteAsync(null);

        // Assert
        vm.TieneError.Should().BeTrue();
        vm.MensajeError.Should().Contain("supera la deuda actual");
        await _clienteService.DidNotReceive().RegistrarCobranzaAsync(Arg.Any<RegistrarCobranzaDto>());
    }

    [Fact]
    public async Task CobrarAsync_EfectivoInsuficiente_MuestraError()
    {
        // Arrange
        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 5000m,
            MedioPago = MedioPagoEnum.Efectivo,
            EfectivoRecibido = 3000m
        };

        // Act
        await vm.CobrarCommand.ExecuteAsync(null);

        // Assert
        vm.TieneError.Should().BeTrue();
        vm.MensajeError.Should().Contain("inferior al monto");
        await _clienteService.DidNotReceive().RegistrarCobranzaAsync(Arg.Any<RegistrarCobranzaDto>());
    }

    [Fact]
    public async Task CobrarAsync_SinTurnoCajaAbierto_MuestraError()
    {
        // Arrange
        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>())
            .Returns((TurnoCajaDto?)null);

        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 5000m
        };

        // Act
        await vm.CobrarCommand.ExecuteAsync(null);

        // Assert
        vm.TieneError.Should().BeTrue();
        vm.MensajeError.Should().Contain("no hay un turno de caja abierto");
        await _clienteService.DidNotReceive().RegistrarCobranzaAsync(Arg.Any<RegistrarCobranzaDto>());
    }

    [Fact]
    public async Task CobrarAsync_Valido_EjecutaRegistrarCobranzaYDisparaRequestClose()
    {
        // Arrange
        var turno = new TurnoCajaDto
        {
            IdTurno = 3,
            IdUsuario = 2,
            FechaApertura = DateTime.UtcNow,
            SaldoInicial = 2000m,
            Estado = EstadoTurnoEnum.Abierto
        };

        _cajaService.ObtenerTurnoActivoAsync(Arg.Any<CancellationToken>())
            .Returns(turno);

        var resultadoEsperado = new CobranzaResultadoDto
        {
            IdCobranza = 105,
            IdCliente = 7,
            ClienteNombre = "Comercial Belgrano",
            FechaHora = DateTime.UtcNow,
            MedioPago = MedioPagoEnum.TarjetaDebito,
            MontoAbonado = 6000m,
            SaldoAnterior = 12000m,
            NuevoSaldo = 6000m,
            Referencia = "Cupón #9841"
        };

        _clienteService.RegistrarCobranzaAsync(Arg.Any<RegistrarCobranzaDto>(), Arg.Any<CancellationToken>())
            .Returns(resultadoEsperado);

        var vm = new CobranzaModalViewModel(_clienteService, _cajaService, _clienteEjemplo)
        {
            MontoCobro = 6000m,
            MedioPago = MedioPagoEnum.TarjetaDebito,
            Referencia = "Cupón #9841"
        };

        bool cerrado = false;
        bool exito = false;
        vm.RequestClose += (s, res) =>
        {
            cerrado = true;
            exito = res;
        };

        // Act
        await vm.CobrarCommand.ExecuteAsync(null);

        // Assert
        cerrado.Should().BeTrue();
        exito.Should().BeTrue();
        vm.Resultado.Should().Be(resultadoEsperado);
        await _clienteService.Received(1).RegistrarCobranzaAsync(
            Arg.Is<RegistrarCobranzaDto>(d =>
                d.IdCliente == 7 &&
                d.IdTurno == 3 &&
                d.IdUsuario == 2 &&
                d.Monto == 6000m &&
                d.MedioPago == MedioPagoEnum.TarjetaDebito &&
                d.Referencia == "Cupón #9841"),
            Arg.Any<CancellationToken>());
    }
}
