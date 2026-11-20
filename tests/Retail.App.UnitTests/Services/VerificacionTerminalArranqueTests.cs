using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.App.ViewModels.Terminales;
using Retail.Application.DTOs.Terminales;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Decisión del arranque según el estado de la terminal (MT-1): sigue, avisa y sigue, activa (Gerente) o bloquea.
/// Los cuadros se reemplazan por un doble, así que no se abre ninguna ventana.
/// </summary>
public class VerificacionTerminalArranqueTests
{
    private readonly ITerminalService _terminalService = Substitute.For<ITerminalService>();
    private readonly ICurrentUserSession _sesion = Substitute.For<ICurrentUserSession>();
    private readonly IDialogosArranqueTerminal _dialogos = Substitute.For<IDialogosArranqueTerminal>();
    private readonly ServiceProvider _proveedor;

    public VerificacionTerminalArranqueTests()
    {
        var local = Substitute.For<ITerminalLocal>();
        local.NombreEquipo.Returns("CAJA-03");

        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => _terminalService);
        servicios.AddTransient(_ => new ActivarTerminalViewModel(
            _terminalService,
            local,
            _sesion,
            NullLogger<ActivarTerminalViewModel>.Instance));
        _proveedor = servicios.BuildServiceProvider();
    }

    private VerificacionTerminalArranque Crear()
    {
        return new VerificacionTerminalArranque(
            _proveedor.GetRequiredService<IServiceScopeFactory>(),
            _sesion,
            _dialogos,
            NullLogger<VerificacionTerminalArranque>.Instance);
    }

    private void Estado(EstadoTerminalEnum estado, TerminalDto? terminal = null)
    {
        _terminalService.VerificarAsync(Arg.Any<CancellationToken>())
            .Returns(new EstadoTerminalDto { Estado = estado, Terminal = terminal });
    }

    [Fact]
    public async Task EjecutarAsync_TerminalActiva_ContinuaSinMostrarNada()
    {
        Estado(EstadoTerminalEnum.Activa, new TerminalDto { Id = 1, Nombre = "Mostrador 1", NombreEquipo = "CAJA-01" });

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeTrue();
        _dialogos.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task EjecutarAsync_EquipoDistinto_AvisaConLosDosNombresYContinua()
    {
        Estado(EstadoTerminalEnum.EquipoDistinto, new TerminalDto { Id = 1, Nombre = "Mostrador 1", NombreEquipo = "CAJA-01" });

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeTrue();
        _dialogos.Received(1).Informar(
            "Esta PC figura como terminal «Mostrador 1», registrada en el equipo «CAJA-01». " +
            "Si la configuración se copió de otra PC, un Gerente debe activarla de nuevo.");
        _dialogos.DidNotReceive().Bloquear(Arg.Any<string>());
    }

    [Theory]
    [InlineData(EstadoTerminalEnum.NoActivada)]
    [InlineData(EstadoTerminalEnum.Inexistente)]
    public async Task EjecutarAsync_NoActivadaSinGerente_AvisaYCierra(EstadoTerminalEnum estado)
    {
        Estado(estado);
        _sesion.EsGerente.Returns(false);

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeFalse();
        _dialogos.Received(1).Bloquear(
            "Esta PC no está activada como terminal de la librería. Un Gerente debe iniciar sesión en ella para activarla.");
        _dialogos.DidNotReceive().ActivarTerminal(Arg.Any<ActivarTerminalViewModel>());
    }

    [Theory]
    [InlineData(EstadoTerminalEnum.NoActivada, false)]
    [InlineData(EstadoTerminalEnum.Inexistente, true)]
    public async Task EjecutarAsync_NoActivadaConGerente_AbreElDialogoYContinuaSiSeActivo(EstadoTerminalEnum estado, bool inexistente)
    {
        Estado(estado);
        _sesion.EsGerente.Returns(true);
        _dialogos.ActivarTerminal(Arg.Any<ActivarTerminalViewModel>()).Returns(true);

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeTrue();
        _dialogos.Received(1).ActivarTerminal(Arg.Is<ActivarTerminalViewModel>(vm => vm.TerminalInexistente == inexistente));
        _dialogos.DidNotReceive().Bloquear(Arg.Any<string>());
    }

    [Fact]
    public async Task EjecutarAsync_GerenteCancelaLaActivacion_CierraLaAplicacion()
    {
        Estado(EstadoTerminalEnum.NoActivada);
        _sesion.EsGerente.Returns(true);
        _dialogos.ActivarTerminal(Arg.Any<ActivarTerminalViewModel>()).Returns(false);

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeFalse();
    }
}
