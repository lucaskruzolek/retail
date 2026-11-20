using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.Application.DTOs.Sesiones;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Latido de la sesión única (MT-6) y aviso de mantenimiento (MT-12). El intervalo se reduce a milisegundos y los
/// cuadros se reemplazan por un doble, así que no hace falta esperar un minuto ni abrir ventanas.
/// </summary>
public class LatidoSesionTests
{
    private const int IdUsuario = 7;
    private const int IdTerminal = 3;
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    private readonly ISesionService _sesionService = Substitute.For<ISesionService>();
    private readonly IAvisosSesion _avisos = Substitute.For<IAvisosSesion>();
    private readonly EstadoMantenimiento _estado = new();
    private readonly ServiceProvider _proveedor;

    public LatidoSesionTests()
    {
        _avisos.AvisarSesionPerdidaYSalirAsync().Returns(Task.CompletedTask);

        var servicios = new ServiceCollection();
        servicios.AddScoped(_ => _sesionService);
        _proveedor = servicios.BuildServiceProvider();
    }

    private LatidoSesion Crear()
    {
        return new LatidoSesion(
            _proveedor.GetRequiredService<IServiceScopeFactory>(),
            _estado,
            _avisos,
            NullLogger<LatidoSesion>.Instance,
            Intervalo);
    }

    private static EstadoLatidoDto Vigente(DateTime? mantenimientoUtc = null, string? motivo = null, DateTime? ahoraServidorUtc = null)
    {
        return new EstadoLatidoDto
        {
            SesionVigente = true,
            MantenimientoProgramadoParaUtc = mantenimientoUtc,
            MotivoMantenimiento = motivo,
            AhoraServidorUtc = ahoraServidorUtc ?? DateTime.UtcNow
        };
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow + Espera;
        while (!condicion())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new TimeoutException("La condición esperada no se cumplió a tiempo.");
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Iniciar_ConSesionVigente_RenuevaEnCadaPulso()
    {
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>()).Returns(Vigente());
        using var latido = Crear();

        latido.Iniciar(IdUsuario, IdTerminal);
        await EsperarAsync(() => _sesionService.ReceivedCalls().Count() >= 3);
        await latido.DetenerAsync();

        latido.EstaActivo.Should().BeFalse();
        await _avisos.DidNotReceive().AvisarSesionPerdidaYSalirAsync();
    }

    [Fact]
    public async Task Iniciar_SesionPerdida_DetieneElBucleYAvisaUnaSolaVez()
    {
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>())
            .Returns(new EstadoLatidoDto { SesionVigente = false, AhoraServidorUtc = DateTime.UtcNow });
        using var latido = Crear();

        latido.Iniciar(IdUsuario, IdTerminal);
        await EsperarAsync(() => !latido.EstaActivo);
        await Task.Delay(100);

        await _avisos.Received(1).AvisarSesionPerdidaYSalirAsync();
        await _sesionService.Received(1).RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Iniciar_ErrorDeBaseEnUnPulso_SigueLatiendo()
    {
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<EstadoLatidoDto>(new InvalidOperationException("Sin conexión con SQL Server")),
                _ => Task.FromResult(Vigente()));
        using var latido = Crear();

        latido.Iniciar(IdUsuario, IdTerminal);
        await EsperarAsync(() => _sesionService.ReceivedCalls().Count() >= 3);

        latido.EstaActivo.Should().BeTrue();
        await latido.DetenerAsync();
        await _avisos.DidNotReceive().AvisarSesionPerdidaYSalirAsync();
    }

    [Fact]
    public async Task Iniciar_AparecerMantenimiento_ActualizaElEstadoYAvisaUnaSolaVez()
    {
        // El servidor dice que el mantenimiento es dentro de 30 minutos: la hora local se arma con ese faltante.
        var ahoraServidor = new DateTime(2026, 11, 19, 20, 0, 0, DateTimeKind.Utc);
        var programado = ahoraServidor.AddMinutes(30);
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>())
            .Returns(Vigente(programado, "Actualización", ahoraServidor));
        using var latido = Crear();
        var antes = DateTime.Now;

        latido.Iniciar(IdUsuario, IdTerminal);
        await EsperarAsync(() => _sesionService.ReceivedCalls().Count() >= 3);
        await latido.DetenerAsync();

        _estado.HayMantenimientoProgramado.Should().BeTrue();
        _estado.Motivo.Should().Be("Actualización");
        _estado.ProgramadoParaLocal.Should().BeCloseTo(antes.AddMinutes(30), TimeSpan.FromSeconds(30));
        _avisos.Received(1).AvisarMantenimiento(Arg.Is<string>(m =>
            m.Contains("Mantenimiento programado a las") && m.Contains("Actualización") && m.Contains("Termine la venta en curso")));
    }

    [Fact]
    public async Task Iniciar_MantenimientoCambiaDeHora_VuelveAAvisar()
    {
        var ahoraServidor = new DateTime(2026, 11, 19, 20, 0, 0, DateTimeKind.Utc);
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(Vigente(ahoraServidor.AddMinutes(30), "Uno", ahoraServidor)),
                _ => Task.FromResult(Vigente(ahoraServidor.AddMinutes(30), "Uno", ahoraServidor)),
                _ => Task.FromResult(Vigente(ahoraServidor.AddMinutes(45), "Uno", ahoraServidor)));
        using var latido = Crear();

        latido.Iniciar(IdUsuario, IdTerminal);
        await EsperarAsync(() => _sesionService.ReceivedCalls().Count() >= 4);
        await latido.DetenerAsync();

        _avisos.Received(2).AvisarMantenimiento(Arg.Any<string>());
    }

    [Fact]
    public async Task Iniciar_MantenimientoDesaparece_LimpiaElEstadoSinAvisar()
    {
        var ahoraServidor = new DateTime(2026, 11, 19, 20, 0, 0, DateTimeKind.Utc);
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromResult(Vigente(ahoraServidor.AddMinutes(30), "Uno", ahoraServidor)),
                _ => Task.FromResult(Vigente()));
        using var latido = Crear();

        latido.Iniciar(IdUsuario, IdTerminal);
        await EsperarAsync(() => _sesionService.ReceivedCalls().Count() >= 3);
        await latido.DetenerAsync();

        _estado.HayMantenimientoProgramado.Should().BeFalse();
        _estado.ProgramadoParaLocal.Should().BeNull();
        _avisos.Received(1).AvisarMantenimiento(Arg.Any<string>());
    }

    [Fact]
    public async Task FinalizarAsync_ConSesionIniciada_CierraLaSesionEnLaBase()
    {
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>()).Returns(Vigente());
        using var latido = Crear();
        latido.Iniciar(IdUsuario, IdTerminal);

        await latido.FinalizarAsync();

        await _sesionService.Received(1).CerrarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizarAsync_ErrorAlCerrar_NoPropagaLaExcepcion()
    {
        _sesionService.RenovarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>()).Returns(Vigente());
        _sesionService.CerrarAsync(IdUsuario, IdTerminal, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sin conexión"));
        using var latido = Crear();
        latido.Iniciar(IdUsuario, IdTerminal);

        var cierre = () => latido.FinalizarAsync();

        await cierre.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FinalizarAsync_SinSesionIniciada_NoCierraNada()
    {
        using var latido = Crear();

        await latido.FinalizarAsync();

        await _sesionService.DidNotReceiveWithAnyArgs().CerrarAsync(default, default, default);
    }
}
