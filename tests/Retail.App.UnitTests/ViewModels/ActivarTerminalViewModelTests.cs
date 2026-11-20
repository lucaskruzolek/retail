using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.App.ViewModels.Terminales;
using Retail.Application.DTOs.Terminales;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

/// <summary>
/// Diálogo "Activar esta PC" (MT-1): validación del formulario, llamada al servicio con el Gerente de la sesión y manejo
/// de errores de negocio dentro del diálogo.
/// </summary>
public class ActivarTerminalViewModelTests
{
    private const int IdGerente = 1;

    private readonly ITerminalService _terminalService = Substitute.For<ITerminalService>();
    private readonly ITerminalLocal _terminalLocal = Substitute.For<ITerminalLocal>();
    private readonly ICurrentUserSession _sesion = Substitute.For<ICurrentUserSession>();

    public ActivarTerminalViewModelTests()
    {
        _terminalLocal.NombreEquipo.Returns("CAJA-03");
        _sesion.IdUsuario.Returns(IdGerente);
        _sesion.EsGerente.Returns(true);
        _terminalService.ListarAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new TerminalDto { Id = 1, Nombre = "Mostrador 1", NombreEquipo = "CAJA-01" },
            new TerminalDto { Id = 2, Nombre = "Depósito", NombreEquipo = string.Empty }
        });
    }

    private ActivarTerminalViewModel Crear()
    {
        return new ActivarTerminalViewModel(_terminalService, _terminalLocal, _sesion, NullLogger<ActivarTerminalViewModel>.Instance);
    }

    [Fact]
    public void TextoIntroduccion_TerminalNoActivada_NombraElEquipo()
    {
        var viewModel = Crear();

        viewModel.TextoIntroduccion.Should().Be("Esta PC (CAJA-03) todavía no es una terminal de la librería.");
    }

    [Fact]
    public void TextoIntroduccion_TerminalInexistente_ExplicaQueLaGuardadaYaNoExiste()
    {
        var viewModel = Crear();

        viewModel.Inicializar(terminalInexistente: true);

        viewModel.TextoIntroduccion.Should().Contain("ya no existe").And.Contain("CAJA-03");
    }

    [Fact]
    public void ActivarCommand_NombreVacio_EstaDeshabilitado()
    {
        var viewModel = Crear();

        viewModel.ActivarCommand.CanExecute(null).Should().BeFalse();

        viewModel.Nombre = "   ";
        viewModel.ActivarCommand.CanExecute(null).Should().BeFalse();

        viewModel.Nombre = "Mostrador 3";
        viewModel.ActivarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task CargarAsync_ListaLasTerminalesYMarcaLasQueNoTienenEquipo()
    {
        var viewModel = Crear();

        await viewModel.CargarAsync();

        viewModel.TerminalesCargadas.Should().BeTrue();
        viewModel.Terminales.Select(t => (t.Nombre, t.TextoEquipo)).Should().Equal(
            ("Mostrador 1", "CAJA-01"),
            ("Depósito", "sin equipo asignado"));
    }

    [Fact]
    public async Task ActivarCommand_ModoReemplazoSinElegir_EstaDeshabilitado()
    {
        var viewModel = Crear();
        await viewModel.CargarAsync();

        viewModel.Modo = ModoActivacionTerminal.Reemplazo;
        viewModel.ActivarCommand.CanExecute(null).Should().BeFalse();

        viewModel.TerminalElegida = viewModel.Terminales[0];
        viewModel.ActivarCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task NoHayTerminalesParaReemplazar_SinTerminalesActivas_EsVerdaderoSoloEnReemplazo()
    {
        _terminalService.ListarAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TerminalDto>());
        var viewModel = Crear();
        await viewModel.CargarAsync();

        viewModel.NoHayTerminalesParaReemplazar.Should().BeFalse();

        viewModel.Modo = ModoActivacionTerminal.Reemplazo;
        viewModel.NoHayTerminalesParaReemplazar.Should().BeTrue();
    }

    [Fact]
    public async Task ActivarCommand_TerminalNueva_LlamaAlServicioConElGerenteYCierraConExito()
    {
        _terminalService.ActivarNuevaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TerminalDto { Id = 5, Nombre = "Mostrador 3", NombreEquipo = "CAJA-03" });
        var viewModel = Crear();
        bool? resultadoDeCierre = null;
        viewModel.RequestClose += (_, resultado) => resultadoDeCierre = resultado;
        viewModel.Nombre = "  Mostrador 3  ";

        await viewModel.ActivarCommand.ExecuteAsync(null);

        await _terminalService.Received(1).ActivarNuevaAsync(IdGerente, "Mostrador 3", Arg.Any<CancellationToken>());
        viewModel.Activada.Should().BeTrue();
        resultadoDeCierre.Should().BeTrue();
        viewModel.TieneError.Should().BeFalse();
    }

    [Fact]
    public async Task ActivarCommand_Reemplazo_LlamaAlServicioConLaTerminalElegida()
    {
        _terminalService.ActivarReemplazoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TerminalDto { Id = 1, Nombre = "Mostrador 1", NombreEquipo = "CAJA-03" });
        var viewModel = Crear();
        await viewModel.CargarAsync();
        viewModel.Modo = ModoActivacionTerminal.Reemplazo;
        viewModel.TerminalElegida = viewModel.Terminales[0];

        await viewModel.ActivarCommand.ExecuteAsync(null);

        await _terminalService.Received(1).ActivarReemplazoAsync(IdGerente, 1, Arg.Any<CancellationToken>());
        await _terminalService.DidNotReceive().ActivarNuevaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        viewModel.Activada.Should().BeTrue();
    }

    [Fact]
    public async Task ActivarCommand_ReglaDeNegocioRechazada_MuestraElMensajeYNoCierra()
    {
        _terminalService.ActivarNuevaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Ya existe una terminal con ese nombre."));
        var viewModel = Crear();
        var cerro = false;
        viewModel.RequestClose += (_, _) => cerro = true;
        viewModel.Nombre = "Mostrador 1";

        await viewModel.ActivarCommand.ExecuteAsync(null);

        viewModel.MensajeError.Should().Be("Ya existe una terminal con ese nombre.");
        viewModel.TieneError.Should().BeTrue();
        viewModel.Activada.Should().BeFalse();
        cerro.Should().BeFalse();
        viewModel.ActivarCommand.CanExecute(null).Should().BeTrue("tras un error se puede corregir el nombre y reintentar");
    }

    [Fact]
    public async Task ActivarCommand_ValidacionDeAplicacion_MuestraLosMensajes()
    {
        _terminalService.ActivarNuevaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException([new ValidationFailure("Nombre", "El nombre no puede superar los 100 caracteres.")]));
        var viewModel = Crear();
        viewModel.Nombre = "Mostrador";

        await viewModel.ActivarCommand.ExecuteAsync(null);

        viewModel.MensajeError.Should().Be("El nombre no puede superar los 100 caracteres.");
    }

    [Fact]
    public async Task ActivarCommand_ErrorInesperado_NoFiltraElDetalleTecnico()
    {
        _terminalService.ActivarNuevaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("detalle interno de SQL"));
        var viewModel = Crear();
        viewModel.Nombre = "Mostrador";

        await viewModel.ActivarCommand.ExecuteAsync(null);

        viewModel.MensajeError.Should().NotContain("SQL").And.Contain("registrado en el log");
        viewModel.Activada.Should().BeFalse();
    }

    [Fact]
    public async Task ActivarCommand_SinUsuarioEnSesion_NoLlamaAlServicio()
    {
        _sesion.IdUsuario.Returns((int?)null);
        var viewModel = Crear();
        viewModel.Nombre = "Mostrador";

        await viewModel.ActivarCommand.ExecuteAsync(null);

        await _terminalService.DidNotReceive().ActivarNuevaAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        viewModel.TieneError.Should().BeTrue();
    }
}
