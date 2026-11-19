using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.UnitTests.Helpers;
using Retail.App.UnitTests.Snapshots;
using Retail.App.ViewModels.Presupuestos;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class RecuperarPresupuestoViewModelTests : IDisposable
{
    private const int IdCliente = 7;
    private const string NombreCliente = "Escuela N.º 12";

    private readonly IPresupuestoService _presupuestoService = Substitute.For<IPresupuestoService>();
    private readonly CargaSerializada _acceso = new();

    public RecuperarPresupuestoViewModelTests()
    {
        ResponderCon(DatosDeMuestra.PresupuestosPendientesDeUnCliente());
    }

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CargarInicial_ConsultaSoloLosPendientes()
    {
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c =>
                c.Estados.SequenceEqual(new[] { FiltroEstadoPresupuestoEnum.Vigentes, FiltroEstadoPresupuestoEnum.Vencidos })
                && c.Pagina == 1
                && c.TamanoPagina == 50),
            Arg.Any<CancellationToken>());
        sut.Presupuestos.Should().HaveCount(3);
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NoConsultaNada()
    {
        _ = CrearViewModel();

        _presupuestoService.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task CargarInicial_ConClienteEnElTicket_ArrancaPrefiltradoPorEl()
    {
        var sut = CrearViewModel(IdCliente, NombreCliente);

        await sut.CargarInicialAsync();

        sut.HayClienteEnElTicket.Should().BeTrue();
        sut.SoloDelCliente.Should().BeTrue();
        sut.TextoSoloDelCliente.Should().Be("Solo de " + NombreCliente);
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.IdsCliente.SequenceEqual(new[] { IdCliente })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DesactivarElInterruptor_ConsultaSinFiltrarPorCliente()
    {
        var sut = CrearViewModel(IdCliente, NombreCliente);
        await sut.CargarInicialAsync();
        _presupuestoService.ClearReceivedCalls();

        sut.SoloDelCliente = false;
        await EsperarCargaAsync(sut);

        await _presupuestoService.Received().ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.IdsCliente.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CargarInicial_SinCliente_NoPrefiltraNiMuestraElInterruptor()
    {
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.HayClienteEnElTicket.Should().BeFalse();
        sut.SoloDelCliente.Should().BeFalse();
        await _presupuestoService.Received(1).ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.IdsCliente.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Buscar_ConTexto_LoMandaAlServidor()
    {
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        _presupuestoService.ClearReceivedCalls();

        sut.TextoBusqueda = "123";
        await EsperarCargaAsync(sut);

        await _presupuestoService.Received().ListarAsync(
            Arg.Is<ConsultaPresupuestosDto>(c => c.TerminoBusqueda == "123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cargar_SeleccionaLaPrimeraFila()
    {
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.PresupuestoSeleccionado.Should().BeSameAs(sut.Presupuestos[0]);
        sut.RecuperarCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public async Task Cargar_ConMasDeLaPagina_AvisaQueSeMuestranLosMasRecientes(int total, bool avisa)
    {
        ResponderCon(DatosDeMuestra.PresupuestosPendientesDeUnCliente(), total);
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.HayMasDeLosMostrados.Should().Be(avisa);
        sut.TextoLimite.Should().Contain("50 más recientes").And.Contain("refine la búsqueda");
    }

    [Fact]
    public async Task Cargar_SinResultados_MuestraElEstadoVacioSegunLaBusqueda()
    {
        ResponderCon([]);
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.EstaVacio.Should().BeTrue();
        sut.TextoVacio.Should().Be("No hay presupuestos pendientes.");
        sut.PresupuestoSeleccionado.Should().BeNull();
        sut.RecuperarCommand.CanExecute(null).Should().BeFalse();

        sut.TextoBusqueda = "zzz";
        sut.TextoVacio.Should().StartWith("Ningún presupuesto pendiente coincide");
    }

    [Fact]
    public async Task Cargar_ConErrorDelServidor_MuestraElErrorYPermiteReintentar()
    {
        _presupuestoService.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Timeout de SQL"));
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.TieneErrorCarga.Should().BeTrue();
        sut.MensajeErrorCarga.Should().NotContain("Timeout");
        sut.EstaVacio.Should().BeFalse();

        ResponderCon(DatosDeMuestra.PresupuestosPendientesDeUnCliente());
        await sut.RecargarCommand.ExecuteAsync(null);

        sut.TieneErrorCarga.Should().BeFalse();
        sut.Presupuestos.Should().HaveCount(3);
    }

    [Fact]
    public void RecuperarCommand_SinSeleccion_EstaDeshabilitado()
    {
        var sut = CrearViewModel();

        sut.RecuperarCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Recuperar_ConLaFilaSeleccionada_DevuelveElPresupuestoElegido()
    {
        var presupuesto = DatosDeMuestra.PresupuestoParaVenta(vigente: true);
        _presupuestoService.RecuperarParaVentaAsync(112, Arg.Any<CancellationToken>()).Returns(presupuesto);
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        sut.PresupuestoSeleccionado = sut.Presupuestos.Single(f => f.IdPresupuesto == 112);

        await sut.RecuperarCommand.ExecuteAsync(null);

        sut.Resultado.Should().BeSameAs(presupuesto);
        sut.Recuperado.Should().BeTrue();
        sut.TieneMensajeError.Should().BeFalse();
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Confirmar_EsperaLaCargaPendienteYRecuperaLaPrimeraFila()
    {
        var presupuesto = DatosDeMuestra.PresupuestoParaVenta(vigente: true);
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>()).Returns(presupuesto);
        var sut = CrearViewModel();

        // Escribir el número y presionar Enter sin esperar a que llegue la lista
        _ = sut.CargarInicialAsync();
        await sut.ConfirmarAsync();

        sut.Resultado.Should().BeSameAs(presupuesto);
    }

    [Theory]
    [InlineData(EstadoPresupuestoEnum.Convertido, "ya se convirtió en una venta")]
    [InlineData(EstadoPresupuestoEnum.Anulado, "está anulado")]
    public async Task Recuperar_YaConvertidoOAnulado_ExplicaElMotivoYRecargaLaLista(EstadoPresupuestoEnum estado, string fragmento)
    {
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TransicionPresupuestoInvalidaException(estado, "recuperar para la venta"));
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        _presupuestoService.ClearReceivedCalls();
        ResponderCon(DatosDeMuestra.PresupuestosPendientesDeUnCliente().Skip(1).ToList());

        await sut.RecuperarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Contain("00000123").And.Contain(fragmento);
        sut.Recuperado.Should().BeFalse();
        await _presupuestoService.Received(1).ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>());
        sut.Presupuestos.Should().HaveCount(2).And.NotContain(f => f.IdPresupuesto == 123);
        sut.PresupuestoSeleccionado.Should().BeSameAs(sut.Presupuestos[0]);
    }

    [Fact]
    public async Task Recuperar_NoExiste_MuestraElMotivoYRecargaLaLista()
    {
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("No se encontró el presupuesto 123."));
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        _presupuestoService.ClearReceivedCalls();

        await sut.RecuperarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().Be("No se encontró el presupuesto 123.");
        await _presupuestoService.Received(1).ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recuperar_ErrorInesperado_MuestraUnMensajeGenericoYPermiteReintentar()
    {
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Timeout de SQL"));
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();

        await sut.RecuperarCommand.ExecuteAsync(null);

        sut.MensajeError.Should().NotBeNullOrEmpty().And.NotContain("Timeout");
        sut.RecuperarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task CambiarLaBusqueda_DespuesDeUnError_LimpiaElMensaje()
    {
        _presupuestoService.RecuperarParaVentaAsync(123, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Timeout de SQL"));
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        await sut.RecuperarCommand.ExecuteAsync(null);

        sut.TextoBusqueda = "1";

        sut.TieneMensajeError.Should().BeFalse();
    }

    private RecuperarPresupuestoViewModel CrearViewModel(int? idCliente = null, string? nombreCliente = null)
    {
        return new RecuperarPresupuestoViewModel(
            _presupuestoService,
            _acceso,
            NullLogger.Instance,
            idCliente,
            nombreCliente,
            new RelojDePrueba(DatosDeMuestra.AhoraPresupuestos));
    }

    private void ResponderCon(IReadOnlyList<PresupuestoResumenDto> items, int? total = null)
    {
        _presupuestoService.ListarAsync(Arg.Any<ConsultaPresupuestosDto>(), Arg.Any<CancellationToken>())
            .Returns(new PresupuestosPaginadosDto
            {
                Items = items,
                TotalRegistros = total ?? items.Count,
                PaginaActual = 1,
                TamanoPagina = 50
            });
    }

    // Una carga disparada por un cambio de propiedad no se puede esperar directamente: se pide otra, que la reemplaza.
    private static Task EsperarCargaAsync(RecuperarPresupuestoViewModel sut)
    {
        return sut.RecargarCommand.ExecuteAsync(null);
    }
}
