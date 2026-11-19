using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.UnitTests.Helpers;
using Retail.App.UnitTests.Snapshots;
using Retail.App.ViewModels.Listas;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class SeleccionarListaViewModelTests : IDisposable
{
    private readonly IListaService _listaService = Substitute.For<IListaService>();
    private readonly CargaSerializada _acceso = new();

    public SeleccionarListaViewModelTests()
    {
        ResponderCon(DatosDeMuestra.Listas());
    }

    public void Dispose()
    {
        _acceso.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_NoConsultaNada()
    {
        _ = CrearViewModel();

        _listaService.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task CargarInicial_ConsultaLaPrimeraPaginaYSeleccionaLaPrimeraFila()
    {
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        await _listaService.Received(1).ListarAsync(
            Arg.Is<ConsultaListasDto>(c => c.Pagina == 1 && c.TamanoPagina == SeleccionarListaViewModel.TamanoPagina),
            Arg.Any<CancellationToken>());
        sut.Listas.Should().HaveCount(5);
        sut.ListaSeleccionada.Should().BeSameAs(sut.Listas[0]);
        sut.IsBusy.Should().BeFalse();
        sut.EstaVacio.Should().BeFalse();
    }

    [Fact]
    public async Task CargarInicial_SinListas_MuestraElEstadoVacio()
    {
        ResponderCon([]);
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.Listas.Should().BeEmpty();
        sut.EstaVacio.Should().BeTrue();
        sut.ListaSeleccionada.Should().BeNull();
        sut.ElegirCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task CargarInicial_ConErrorDelServicio_MuestraElErrorDeCargaYNoElEstadoVacio()
    {
        _listaService.ListarAsync(Arg.Any<ConsultaListasDto>(), Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("sin conexión"));
        var sut = CrearViewModel();

        await sut.CargarInicialAsync();

        sut.TieneErrorCarga.Should().BeTrue();
        sut.EstaVacio.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmarAsync_ConUnaFilaSeleccionada_ElIgeLaListaYMarcaElDialogoParaCerrar()
    {
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        sut.ListaSeleccionada = sut.Listas[1];

        await sut.ConfirmarAsync();

        sut.IdListaElegida.Should().Be(11);
        sut.Elegida.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmarAsync_SinFilas_NoEligeNada()
    {
        ResponderCon([]);
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();

        await sut.ConfirmarAsync();

        sut.IdListaElegida.Should().BeNull();
        sut.Elegida.Should().BeFalse();
    }

    [Fact]
    public async Task TextoBusqueda_AlEscribir_ConsultaPorEseTextoEnElServidor()
    {
        var sut = CrearViewModel();
        await sut.CargarInicialAsync();
        _listaService.ClearReceivedCalls();

        sut.TextoBusqueda = "jardín";
        await sut.ConfirmarAsync();

        await _listaService.Received().ListarAsync(
            Arg.Is<ConsultaListasDto>(c => c.TerminoBusqueda == "jardín"),
            Arg.Any<CancellationToken>());
    }

    private SeleccionarListaViewModel CrearViewModel()
    {
        return new SeleccionarListaViewModel(
            _listaService,
            _acceso,
            NullLogger.Instance,
            new RelojDePrueba(DatosDeMuestra.AhoraListas));
    }

    private void ResponderCon(IReadOnlyList<ListaResumenDto> listas)
    {
        _listaService.ListarAsync(Arg.Any<ConsultaListasDto>(), Arg.Any<CancellationToken>())
            .Returns(new ListasPaginadosDto
            {
                Items = listas,
                TotalRegistros = listas.Count,
                PaginaActual = 1,
                TamanoPagina = SeleccionarListaViewModel.TamanoPagina
            });
    }
}
