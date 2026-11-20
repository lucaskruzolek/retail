using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Retail.App.Services;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Registro de la sesión única al arrancar (MT-3): inicia, rechaza o retoma una sesión vencida. Los cuadros se
/// reemplazan por un doble, así que no se abre ninguna ventana.
/// </summary>
public class InicioSesionUnicaTests
{
    private const int IdUsuario = 7;
    private const int IdTerminal = 3;

    private readonly ISesionService _sesionService = Substitute.For<ISesionService>();
    private readonly ICurrentUserSession _sesion = Substitute.For<ICurrentUserSession>();
    private readonly ITerminalLocal _terminalLocal = Substitute.For<ITerminalLocal>();
    private readonly IAvisosSesion _avisos = Substitute.For<IAvisosSesion>();
    private readonly ServiceProvider _proveedor;

    public InicioSesionUnicaTests()
    {
        _sesion.IdUsuario.Returns(IdUsuario);
        _sesion.NombreCompleto.Returns("Juan Pérez");
        _terminalLocal.IdTerminal.Returns(IdTerminal);

        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => _sesionService);
        _proveedor = servicios.BuildServiceProvider();
    }

    private InicioSesionUnica Crear()
    {
        return new InicioSesionUnica(
            _proveedor.GetRequiredService<IServiceScopeFactory>(),
            _sesion,
            _terminalLocal,
            _avisos,
            NullLogger<InicioSesionUnica>.Instance);
    }

    private void Resultado(ResultadoInicioSesionDto resultado)
    {
        _sesionService.IniciarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>()).Returns(resultado);
    }

    [Fact]
    public async Task EjecutarAsync_SesionIniciada_ContinuaSinMostrarNada()
    {
        Resultado(new ResultadoInicioSesionDto { Iniciada = true });

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeTrue();
        _avisos.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task EjecutarAsync_TomoSesionVencida_ContinuaYAvisaQueSeRetomo()
    {
        Resultado(new ResultadoInicioSesionDto { Iniciada = true, TomoSesionVencida = true });

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeTrue();
        _avisos.Received(1).Informar(Arg.Is<string>(m => m.Contains("Se retomó la sesión")));
        _avisos.DidNotReceive().RechazarInicio(Arg.Any<string>());
    }

    [Fact]
    public async Task EjecutarAsync_SesionVigenteEnOtraTerminal_RechazaNombrandoTerminalYHora()
    {
        var inicioUtc = new DateTime(2026, 11, 19, 14, 30, 0, DateTimeKind.Utc);
        Resultado(new ResultadoInicioSesionDto { Iniciada = false, TerminalOcupada = "Mostrador 2", InicioOtraSesionUtc = inicioUtc });
        var horaLocal = inicioUtc.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture);

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeFalse();
        _avisos.Received(1).RechazarInicio(Arg.Is<string>(m =>
            m.Contains("Juan Pérez ya tiene una sesión abierta en la terminal «Mostrador 2»") &&
            m.Contains($"desde las {horaLocal}") &&
            m.Contains("espere 3 minutos")));
    }

    [Fact]
    public async Task EjecutarAsync_RechazoSinNombreDeTerminal_DiceEnOtraTerminal()
    {
        Resultado(new ResultadoInicioSesionDto { Iniciada = false, TerminalOcupada = null });

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeFalse();
        _avisos.Received(1).RechazarInicio(Arg.Is<string>(m => m.Contains("ya tiene una sesión abierta en otra terminal")));
    }

    [Fact]
    public async Task EjecutarAsync_SinTerminalIdentificada_RechazaSinConsultarLaBase()
    {
        _terminalLocal.IdTerminal.Returns((int?)null);

        var continuar = await Crear().EjecutarAsync();

        continuar.Should().BeFalse();
        _avisos.Received(1).RechazarInicio(Arg.Any<string>());
        await _sesionService.DidNotReceiveWithAnyArgs().IniciarAsync(default, default, default);
    }
}
