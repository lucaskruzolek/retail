using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Retail.App.Services;
using Retail.Application.Interfaces.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Espera previa a restaurar con otras terminales conectadas (MT-12). El reloj y la espera son simulados: cada "espera" de
/// 5 segundos adelanta el reloj sin demorar la prueba.
/// </summary>
public class OrquestadorMantenimientoTests
{
    private const int IdActor = 7;

    private readonly IRestauracionService _restauracion = Substitute.For<IRestauracionService>();
    private readonly ISesionService _sesiones = Substitute.For<ISesionService>();
    private readonly IBackupDialogService _dialogos = Substitute.For<IBackupDialogService>();
    private readonly RelojManual _reloj = new();
    private readonly List<string> _progreso = [];

    private OrquestadorMantenimiento CrearSut()
    {
        return new OrquestadorMantenimiento(
            _restauracion,
            _sesiones,
            _dialogos,
            _reloj,
            (intervalo, token) =>
            {
                token.ThrowIfCancellationRequested();
                _reloj.Avanzar(intervalo);
                return Task.CompletedTask;
            });
    }

    private Task<ResultadoPreparacionMantenimiento> Preparar(OrquestadorMantenimiento sut, CancellationToken token = default)
    {
        return sut.PrepararAsync(IdActor, new ProgresoSincronico(_progreso), token);
    }

    [Fact]
    public async Task PrepararAsync_SinTerminalesConectadas_ContinuaSinProgramarNada()
    {
        // Arrange
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>()).Returns(0);

        // Act
        var resultado = await Preparar(CrearSut());

        // Assert
        resultado.Should().Be(new ResultadoPreparacionMantenimiento(true, false));
        _dialogos.DidNotReceive().SolicitarProgramacionMantenimiento(Arg.Any<int>());
        await _sesiones.DidNotReceive().ProgramarMantenimientoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepararAsync_LasTerminalesSeVan_ContinuaConElMantenimientoProgramado()
    {
        // Arrange
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>()).Returns(2, 2, 1, 0);
        _dialogos.SolicitarProgramacionMantenimiento(2).Returns(2);

        // Act
        var resultado = await Preparar(CrearSut());

        // Assert
        resultado.Should().Be(new ResultadoPreparacionMantenimiento(true, true));
        await _sesiones.Received(1).ProgramarMantenimientoAsync(IdActor, 2, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _sesiones.DidNotReceive().CancelarMantenimientoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        _progreso.Should().NotBeEmpty();
        _progreso[0].Should().StartWith("Esperando que se desconecten 2 terminal(es)").And.Contain("(hasta las ");
        _dialogos.DidNotReceive().ConfirmarRestaurarConTerminalesConectadas(Arg.Any<int>());
    }

    [Fact]
    public async Task PrepararAsync_VenceElPlazoYElOperadorConfirma_ContinuaConLasTerminalesConectadas()
    {
        // Arrange: nunca se van; con 2 minutos de plazo y 1 de gracia, el reloj vence tras 3 minutos de sondeos de 5 segundos
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>()).Returns(1);
        _dialogos.SolicitarProgramacionMantenimiento(1).Returns(2);
        _dialogos.ConfirmarRestaurarConTerminalesConectadas(1).Returns(true);

        // Act
        var resultado = await Preparar(CrearSut());

        // Assert
        resultado.Should().Be(new ResultadoPreparacionMantenimiento(true, true));
        _dialogos.Received(1).ConfirmarRestaurarConTerminalesConectadas(1);
        _reloj.Transcurrido.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMinutes(3));
        await _sesiones.DidNotReceive().CancelarMantenimientoAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepararAsync_VenceElPlazoYElOperadorRechaza_CancelaElMantenimientoYNoContinua()
    {
        // Arrange
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>()).Returns(1);
        _dialogos.SolicitarProgramacionMantenimiento(1).Returns(0);
        _dialogos.ConfirmarRestaurarConTerminalesConectadas(1).Returns(false);

        // Act
        var resultado = await Preparar(CrearSut());

        // Assert
        resultado.Should().Be(new ResultadoPreparacionMantenimiento(false, false));
        await _sesiones.Received(1).CancelarMantenimientoAsync(IdActor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepararAsync_ElOperadorCancelaLaEspera_CancelaElMantenimientoYNoContinua()
    {
        // Arrange
        using var cancelacion = new CancellationTokenSource();
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>()).Returns(1);
        _dialogos.SolicitarProgramacionMantenimiento(1).Returns(5);
        var sut = new OrquestadorMantenimiento(
            _restauracion,
            _sesiones,
            _dialogos,
            _reloj,
            (_, token) =>
            {
                cancelacion.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        // Act
        var resultado = await sut.PrepararAsync(IdActor, null, cancelacion.Token);

        // Assert
        resultado.Should().Be(new ResultadoPreparacionMantenimiento(false, false));
        await _sesiones.Received(1).CancelarMantenimientoAsync(IdActor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepararAsync_ElOperadorNoElijeMinutos_NoProgramaNada()
    {
        // Arrange
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>()).Returns(2);
        _dialogos.SolicitarProgramacionMantenimiento(2).Returns((int?)null);

        // Act
        var resultado = await Preparar(CrearSut());

        // Assert
        resultado.Should().Be(new ResultadoPreparacionMantenimiento(false, false));
        await _sesiones.DidNotReceive().ProgramarMantenimientoAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrepararAsync_ContarFallaMientrasEspera_CancelaElMantenimientoYPropagaElError()
    {
        // Arrange
        _restauracion.ContarTerminalesConectadasAsync(IdActor, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1), Task.FromException<int>(new InvalidOperationException("sin conexión")));
        _dialogos.SolicitarProgramacionMantenimiento(1).Returns(2);

        // Act
        var accion = () => Preparar(CrearSut());

        // Assert
        await accion.Should().ThrowAsync<InvalidOperationException>().WithMessage("sin conexión");
        await _sesiones.Received(1).CancelarMantenimientoAsync(IdActor, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelarAsync_ElServicioFalla_NoPropagaElError()
    {
        // Arrange
        _sesiones.CancelarMantenimientoAsync(IdActor, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sin conexión"));

        // Act
        var accion = () => CrearSut().CancelarAsync(IdActor);

        // Assert
        await accion.Should().NotThrowAsync();
    }

    /// <summary>Reloj que solo avanza cuando la espera simulada lo pide.</summary>
    private sealed class RelojManual : TimeProvider
    {
        private readonly DateTimeOffset _inicio = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);
        private DateTimeOffset _ahora;

        public RelojManual()
        {
            _ahora = _inicio;
        }

        public TimeSpan Transcurrido => _ahora - _inicio;

        public void Avanzar(TimeSpan tiempo)
        {
            _ahora += tiempo;
        }

        public override DateTimeOffset GetUtcNow() => _ahora;
    }

    /// <summary>Progreso que registra en el acto (Progress&lt;T&gt; publica en otro hilo).</summary>
    private sealed class ProgresoSincronico(List<string> mensajes) : IProgress<string>
    {
        public void Report(string value)
        {
            mensajes.Add(value);
        }
    }
}
