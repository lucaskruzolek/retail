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

public class DetalleMarcaViewModelTests : IDisposable
{
    private const int IdActor = 7;

    private readonly IMarcaService _marcaService = Substitute.For<IMarcaService>();
    private readonly IClasificacionDialogService _dialogService = Substitute.For<IClasificacionDialogService>();
    private readonly ICurrentUserSession _session = Substitute.For<ICurrentUserSession>();
    private readonly CargaSerializada _carga = new();
    private readonly DetalleMarcaViewModel _sut;

    private readonly MarcaDto _rivadavia = new() { IdMarca = 20, NombreMarca = "Rivadavia" };
    private readonly AliasListadoDto _unAlias = new() { IdMarca = 20, NombreMarca = "Rivadavia", Alias = "RIV" };
    private readonly PrefijoListadoDto _unPrefijo = new() { IdMarca = 20, NombreMarca = "Rivadavia", Prefijo = "7790001" };

    public DetalleMarcaViewModelTests()
    {
        _session.IdUsuario.Returns(IdActor);
        _session.EsGerente.Returns(true);

        _marcaService.ListarAliasAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<AliasListadoDto>>([_unAlias]));
        _marcaService.ListarPrefijosAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PrefijoListadoDto>>([_unPrefijo]));

        _sut = new DetalleMarcaViewModel(_marcaService, _dialogService, _session, _carga, NullLogger.Instance);
    }

    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task MostrarAsync_ConMarca_CargaSusAliasYPrefijosFiltradosPorLaMarca()
    {
        await _sut.MostrarAsync(_rivadavia);

        _sut.HayMarca.Should().BeTrue();
        _sut.Alias.Should().ContainSingle().Which.Alias.Should().Be("RIV");
        _sut.Prefijos.Should().ContainSingle().Which.Prefijo.Should().Be("7790001");
        await _marcaService.Received(1).ListarAliasAsync(null, 20, Arg.Any<CancellationToken>());
        await _marcaService.Received(1).ListarPrefijosAsync(null, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MostrarAsync_SinMarca_VaciaElDetalleSinConsultar()
    {
        await _sut.MostrarAsync(_rivadavia);
        _marcaService.ClearReceivedCalls();

        await _sut.MostrarAsync(null);

        _sut.HayMarca.Should().BeFalse();
        _sut.Alias.Should().BeEmpty();
        _sut.Prefijos.Should().BeEmpty();
        await _marcaService.DidNotReceiveWithAnyArgs().ListarAliasAsync(default, default, default);
    }

    [Fact]
    public async Task NuevoAlias_ConFormularioAceptado_LoAgregaALaMarcaSeleccionadaYRecarga()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.AbrirFormularioAlias(_rivadavia).Returns(new ResultadoFormularioDiccionario("RIVADAVIA S.A.", 20));

        await _sut.NuevoAliasCommand.ExecuteAsync(null);

        await _marcaService.Received(1).AgregarAliasAsync(
            IdActor,
            Arg.Is<AliasMarcaDto>(d => d.IdMarca == 20 && d.Alias == "RIVADAVIA S.A."),
            Arg.Any<CancellationToken>());
        await _marcaService.Received(2).ListarAliasAsync(null, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NuevoAlias_ConAliasDeOtraMarca_MuestraElErrorDeDominio()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.AbrirFormularioAlias(_rivadavia).Returns(new ResultadoFormularioDiccionario("BIC", 20));
        _marcaService.AgregarAliasAsync(IdActor, Arg.Any<AliasMarcaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El alias ya es de otra marca."));

        await _sut.NuevoAliasCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("El alias ya es de otra marca.");
    }

    [Fact]
    public async Task NuevoAlias_FormularioCancelado_NoLlamaAlServicio()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.AbrirFormularioAlias(_rivadavia).Returns((ResultadoFormularioDiccionario?)null);

        await _sut.NuevoAliasCommand.ExecuteAsync(null);

        await _marcaService.DidNotReceiveWithAnyArgs().AgregarAliasAsync(default, default!, default);
    }

    [Fact]
    public async Task QuitarAlias_ConConfirmacion_LlamaAQuitarAlias()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.QuitarAliasCommand.ExecuteAsync(_unAlias);

        await _marcaService.Received(1).QuitarAliasAsync(
            IdActor,
            Arg.Is<AliasMarcaDto>(d => d.IdMarca == 20 && d.Alias == "RIV"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QuitarAlias_SinConfirmacion_NoLlamaAlServicio()
    {
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.QuitarAliasCommand.ExecuteAsync(_unAlias);

        await _marcaService.DidNotReceiveWithAnyArgs().QuitarAliasAsync(default, default!, default);
    }

    [Fact]
    public async Task NuevoPrefijo_ConFormularioAceptado_LoAgregaALaMarcaSeleccionada()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.AbrirFormularioPrefijo(_rivadavia).Returns(new ResultadoFormularioDiccionario("7790002", 20));

        await _sut.NuevoPrefijoCommand.ExecuteAsync(null);

        await _marcaService.Received(1).AgregarPrefijoAsync(
            IdActor,
            Arg.Is<PrefijoMarcaDto>(d => d.IdMarca == 20 && d.Prefijo == "7790002"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NuevoPrefijo_ConErrorDeDominio_MuestraElMensaje()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.AbrirFormularioPrefijo(_rivadavia).Returns(new ResultadoFormularioDiccionario("7790001", 20));
        _marcaService.AgregarPrefijoAsync(IdActor, Arg.Any<PrefijoMarcaDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("El prefijo ya existe."));

        await _sut.NuevoPrefijoCommand.ExecuteAsync(null);

        _sut.MensajeError.Should().Be("El prefijo ya existe.");
    }

    [Fact]
    public async Task QuitarPrefijo_ConConfirmacion_LlamaAQuitarPrefijo()
    {
        await _sut.MostrarAsync(_rivadavia);
        _dialogService.Confirmar(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await _sut.QuitarPrefijoCommand.ExecuteAsync(_unPrefijo);

        await _marcaService.Received(1).QuitarPrefijoAsync(
            IdActor,
            Arg.Is<PrefijoMarcaDto>(d => d.IdMarca == 20 && d.Prefijo == "7790001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Agregar_SinMarcaSeleccionada_QuedaDeshabilitado()
    {
        _sut.NuevoAliasCommand.CanExecute(null).Should().BeFalse();
        _sut.NuevoPrefijoCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Escritura_SinPermisoDeClasificacion_QuedaDeshabilitada()
    {
        _session.EsGerente.Returns(false);
        _session.EsEncargado.Returns(false);
        await _sut.MostrarAsync(_rivadavia);

        _sut.NuevoAliasCommand.CanExecute(null).Should().BeFalse();
        _sut.QuitarAliasCommand.CanExecute(_unAlias).Should().BeFalse();
        _sut.NuevoPrefijoCommand.CanExecute(null).Should().BeFalse();
        _sut.QuitarPrefijoCommand.CanExecute(_unPrefijo).Should().BeFalse();
    }
}
