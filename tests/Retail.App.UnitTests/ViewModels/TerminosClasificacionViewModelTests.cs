using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Articulos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class TerminosClasificacionViewModelTests : IDisposable
{
    private const int IdActor = 7;

    private readonly ITerminoClasificacionService _terminoService = Substitute.For<ITerminoClasificacionService>();
    private readonly ICategoriaService _categoriaService = Substitute.For<ICategoriaService>();
    private readonly IClasificacionDialogService _dialogService = Substitute.For<IClasificacionDialogService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly CargaSerializada _carga = new();
    private readonly TerminosClasificacionViewModel _sut;

    private readonly TerminoDto _ambiguo = new() { IdTermino = 1, Termino = "CUADERNO" };
    private readonly TerminoDto _conCategoria = new() { IdTermino = 2, Termino = "LAPIZ", IdCategoria = 10, NombreCategoria = "Lápices" };

    private readonly List<CategoriaDto> _categorias =
    [
        new() { IdCategoria = 10, NombreCategoria = "Lápices", IdRubro = 1 },
        new() { IdCategoria = 11, NombreCategoria = "Cuadernos", IdRubro = 1 }
    ];

    public TerminosClasificacionViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _session.EsGerente.Returns(true);

        _categoriaService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CategoriaDto>>(_categorias));
        _terminoService.ListarAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TerminoDto>>([_ambiguo, _conCategoria]));
        _terminoService.ListarParesAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParDto>>([]));

        _sut = new TerminosClasificacionViewModel(
            _terminoService, _categoriaService, _dialogService, _session, _carga, NullLogger.Instance);
    }

    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        for (int i = 0; i < 100 && !condicion(); i++)
        {
            await Task.Delay(20);
        }

        condicion().Should().BeTrue("la condición debía cumplirse tras la carga asíncrona");
    }

    private static ParDto Par(string segundaPalabra, int? idCategoria = null)
    {
        return new ParDto { IdTermino = 1, Termino = "CUADERNO", SegundaPalabra = segundaPalabra, IdCategoria = idCategoria };
    }

    [Fact]
    public async Task InicializarAsync_ConTerminos_CargaTerminosYCategoriasConLaOpcionTodas()
    {
        await _sut.InicializarAsync();

        _sut.Terminos.Should().HaveCount(2);
        _sut.CategoriasFiltro.Should().HaveCount(3);
        _sut.CategoriasFiltro[0].IdCategoria.Should().Be(0);
        _sut.CategoriaFiltro.Should().Be(_sut.CategoriasFiltro[0]);
        _sut.MensajeError.Should().BeNull();
    }

    [Fact]
    public async Task SoloAmbiguos_AlActivarse_PideAlServicioSoloLosAmbiguos()
    {
        await _sut.InicializarAsync();

        _sut.SoloAmbiguos = true;

        await EsperarAsync(() => _terminoService.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ITerminoClasificacionService.ListarAsync) &&
            c.GetArguments()[2] is true));
    }

    [Fact]
    public async Task Filtro_ConTexto_PideAlServicioElTextoNormalizado()
    {
        await _sut.InicializarAsync();

        _sut.Filtro = "  cuad ";

        await EsperarAsync(() => _terminoService.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ITerminoClasificacionService.ListarAsync) &&
            c.GetArguments()[0] is "cuad"));
    }

    [Fact]
    public async Task CategoriaFiltro_AlElegirUnaCategoria_PideAlServicioEsaCategoria()
    {
        await _sut.InicializarAsync();

        _sut.CategoriaFiltro = _sut.CategoriasFiltro[1];

        await EsperarAsync(() => _terminoService.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ITerminoClasificacionService.ListarAsync) &&
            c.GetArguments()[1] is 10));
    }

    [Fact]
    public async Task TerminoSeleccionado_Ambiguo_CargaSusPares()
    {
        _terminoService.ListarParesAsync(1, Arg.Any<string?>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParDto>>([Par("ESPIRAL", 11), Par("TAPA")]));
        await _sut.InicializarAsync();

        _sut.TerminoSeleccionado = _ambiguo;

        await EsperarAsync(() => _sut.Pares.Count == 2);
        _sut.HayTerminoAmbiguoSeleccionado.Should().BeTrue();
    }

    [Fact]
    public async Task TerminoSeleccionado_ConCategoria_NoOfreceParesYLosLimpia()
    {
        await _sut.InicializarAsync();

        _sut.TerminoSeleccionado = _conCategoria;

        _sut.HayTerminoAmbiguoSeleccionado.Should().BeFalse();
        _sut.Pares.Should().BeEmpty();
    }

    [Fact]
    public async Task NuevoTermino_ConFormularioAceptado_RegistraElTerminoConElActor()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioTermino(null, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("MOCHILA", 11));

        await _sut.NuevoTerminoCommand.ExecuteAsync(null);

        await _terminoService.Received(1).RegistrarAsync(
            IdActor,
            Arg.Is<RegistrarTerminoDto>(d => d.Termino == "MOCHILA" && d.IdCategoria == 11),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NuevoTermino_ConFormularioCancelado_NoLlamaAlServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioTermino(null, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns((ResultadoFormularioDiccionario?)null);

        await _sut.NuevoTerminoCommand.ExecuteAsync(null);

        await _terminoService.DidNotReceiveWithAnyArgs().RegistrarAsync(default, default!, default);
    }

    [Fact]
    public async Task NuevoTermino_ConErrorDeDominio_MuestraElMensajeSinCerrar()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioTermino(null, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("MOCHILA", null));
        _terminoService.RegistrarAsync(IdActor, Arg.Any<RegistrarTerminoDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Otro usuario registró el término."));

        await _sut.NuevoTerminoCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("Otro usuario registró el término.");
        _sut.TieneError.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarCategoria_TerminoAmbiguoConPares_PideConfirmacionConLaCantidad()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioTermino(_ambiguo, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("CUADERNO", 11));
        _terminoService.ListarParesAsync(1, Arg.Any<string?>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParDto>>([Par("ESPIRAL", 11), Par("TAPA")]));
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.CambiarCategoriaCommand.ExecuteAsync(_ambiguo);

        _dialogService.Received(1).Confirmar(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("2 par(es)")));
        await _terminoService.Received(1).RegistrarAsync(
            IdActor,
            Arg.Is<RegistrarTerminoDto>(d => d.Termino == "CUADERNO" && d.IdCategoria == 11),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CambiarCategoria_SiElUsuarioRechazaElAviso_NoGuarda()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioTermino(_ambiguo, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("CUADERNO", 11));
        _terminoService.ListarParesAsync(1, Arg.Any<string?>(), true, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParDto>>([Par("ESPIRAL", 11)]));
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.CambiarCategoriaCommand.ExecuteAsync(_ambiguo);

        await _terminoService.DidNotReceiveWithAnyArgs().RegistrarAsync(default, default!, default);
    }

    [Fact]
    public async Task CambiarCategoria_TerminoAmbiguoSinPares_GuardaSinPedirConfirmacion()
    {
        await _sut.InicializarAsync();
        _dialogService.AbrirFormularioTermino(_ambiguo, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("CUADERNO", 11));

        await _sut.CambiarCategoriaCommand.ExecuteAsync(_ambiguo);

        _dialogService.DidNotReceive().Confirmar(Arg.Any<string>(), Arg.Any<string>());
        await _terminoService.Received(1).RegistrarAsync(IdActor, Arg.Any<RegistrarTerminoDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaTermino_ConConfirmacion_LlamaALaBajaDelServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.BajaTerminoCommand.ExecuteAsync(_conCategoria);

        await _terminoService.Received(1).BajaAsync(IdActor, 2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BajaTermino_SinConfirmacion_NoLlamaAlServicio()
    {
        await _sut.InicializarAsync();
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.BajaTerminoCommand.ExecuteAsync(_conCategoria);

        await _terminoService.DidNotReceiveWithAnyArgs().BajaAsync(default, default, default);
    }

    [Fact]
    public async Task NuevoPar_ConDestinoIgnorado_AsignaElParSinCategoria()
    {
        await _sut.InicializarAsync();
        _sut.TerminoSeleccionado = _ambiguo;
        _dialogService.AbrirFormularioPar(_ambiguo, null, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("TAPA", null));

        await _sut.NuevoParCommand.ExecuteAsync(null);

        await _terminoService.Received(1).AsignarParAsync(
            IdActor,
            Arg.Is<AsignarParDto>(d => d.IdTermino == 1 && d.SegundaPalabra == "TAPA" && d.IdCategoria == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditarPar_ConNuevaCategoria_ReasignaElPar()
    {
        await _sut.InicializarAsync();
        _sut.TerminoSeleccionado = _ambiguo;
        var par = Par("ESPIRAL");
        _dialogService.AbrirFormularioPar(_ambiguo, par, Arg.Any<IReadOnlyList<CategoriaDto>>())
            .Returns(new ResultadoFormularioDiccionario("ESPIRAL", 11));

        await _sut.EditarParCommand.ExecuteAsync(par);

        await _terminoService.Received(1).AsignarParAsync(
            IdActor,
            Arg.Is<AsignarParDto>(d => d.SegundaPalabra == "ESPIRAL" && d.IdCategoria == 11),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuitarPar_ConConfirmacion_LlamaAQuitarParDelServicio()
    {
        await _sut.InicializarAsync();
        _sut.TerminoSeleccionado = _ambiguo;
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.QuitarParCommand.ExecuteAsync(Par("ESPIRAL", 11));

        await _terminoService.Received(1).QuitarParAsync(
            IdActor,
            Arg.Is<QuitarParDto>(d => d.IdTermino == 1 && d.SegundaPalabra == "ESPIRAL"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Comandos_SinPermisoDeClasificacion_QuedanDeshabilitados()
    {
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(false);

        _sut.PuedeEscribir.Should().BeFalse();
        _sut.NuevoTerminoCommand.CanExecute(null).Should().BeFalse();
        _sut.CambiarCategoriaCommand.CanExecute(_ambiguo).Should().BeFalse();
        _sut.BajaTerminoCommand.CanExecute(_ambiguo).Should().BeFalse();
        _sut.QuitarParCommand.CanExecute(Par("TAPA")).Should().BeFalse();
    }

    [Fact]
    public void Comandos_ConEncargado_QuedanHabilitados()
    {
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(true);

        _sut.NuevoTerminoCommand.CanExecute(null).Should().BeTrue();
    }

    // ---------------------------------------------------- Pares propuestos (M-71)

    private static ParPropuestoDto Propuesto(string segundaPalabra, int idCategoria)
    {
        return new ParPropuestoDto
        {
            IdTermino = 1,
            Termino = "CUADERNO",
            SegundaPalabra = segundaPalabra,
            IdCategoria = idCategoria,
            NombreCategoria = "Cuadernos",
            Coincidencias = 3,
            Total = 3,
            Ejemplos = [$"CUADERNO {segundaPalabra}"]
        };
    }

    private async Task SeleccionarElAmbiguoAsync()
    {
        await _sut.InicializarAsync();
        _sut.TerminoSeleccionado = _sut.Terminos.Single(t => t.IdTermino == _ambiguo.IdTermino);
        await EsperarAsync(() => !_carga.EnCurso);
    }

    [Fact]
    public async Task MostrandoPropuestos_ConTerminoAmbiguo_CargaSoloLasPropuestasDeEseTermino()
    {
        _terminoService.ListarParesPropuestosAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParPropuestoDto>>([Propuesto("TAPA", 11)]));
        await SeleccionarElAmbiguoAsync();
        _terminoService.ClearReceivedCalls();

        _sut.MostrandoPropuestos = true;

        await EsperarAsync(() => _sut.Propuestos.HayPropuestas);
        _sut.Propuestos.Propuestas.Single().CategoriaElegida.IdCategoria.Should().Be(11);
        await _terminoService.Received(1).ListarParesPropuestosAsync(1, Arg.Any<CancellationToken>());
        await _terminoService.DidNotReceiveWithAnyArgs().ListarParesAsync(default, default, default, default);
    }

    [Fact]
    public async Task MostrandoPropuestos_EnLaPestanaDePares_NoCalculaPropuestas()
    {
        await SeleccionarElAmbiguoAsync();

        await _terminoService.DidNotReceiveWithAnyArgs().ListarParesPropuestosAsync(default, default);
        await _terminoService.Received().ListarParesAsync(1, null, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MostrandoPropuestos_AlVolverAPares_RecargaLosPares()
    {
        await SeleccionarElAmbiguoAsync();
        _sut.MostrandoPropuestos = true;
        await EsperarAsync(() => !_carga.EnCurso);
        _terminoService.ListarParesAsync(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParDto>>([Par("ESPIRAL", 11)]));

        _sut.MostrandoPropuestos = false;

        await EsperarAsync(() => _sut.Pares.Count == 1);
    }

    [Fact]
    public async Task Propuestos_AlAceptarUnPar_RecargaLasPropuestasDelTermino()
    {
        _terminoService.ListarParesPropuestosAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParPropuestoDto>>([Propuesto("TAPA", 11)]));
        await SeleccionarElAmbiguoAsync();
        _sut.MostrandoPropuestos = true;
        await EsperarAsync(() => _sut.Propuestos.HayPropuestas);
        _terminoService.ListarParesPropuestosAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParPropuestoDto>>([]));

        await _sut.Propuestos.AceptarCommand.ExecuteAsync(_sut.Propuestos.Propuestas[0]);

        await EsperarAsync(() => !_sut.Propuestos.HayPropuestas);
        await _terminoService.Received(1).AsignarParesAsync(IdActor, Arg.Any<AsignarParesDto>(), Arg.Any<CancellationToken>());
    }
}
