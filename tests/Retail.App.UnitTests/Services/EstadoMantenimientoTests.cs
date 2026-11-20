using FluentAssertions;
using Retail.App.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Estado del mantenimiento programado (MT-12): avisa los cambios y bloquea los cobros desde el minuto previo.
/// </summary>
public class EstadoMantenimientoTests
{
    private sealed class RelojFijo : TimeProvider
    {
        private readonly DateTimeOffset _ahora;

        public RelojFijo(DateTime ahoraLocal)
        {
            _ahora = new DateTimeOffset(ahoraLocal, TimeSpan.Zero);
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _ahora;
    }

    private static readonly DateTime Ahora = new(2026, 11, 19, 20, 0, 0);

    [Fact]
    public void Establecer_DisparaCambioYExponeLosDatos()
    {
        var estado = new EstadoMantenimiento(new RelojFijo(Ahora));
        var cambios = 0;
        estado.Cambio += () => cambios++;

        estado.Establecer(Ahora.AddMinutes(30), "Backup");

        cambios.Should().Be(1);
        estado.HayMantenimientoProgramado.Should().BeTrue();
        estado.ProgramadoParaLocal.Should().Be(Ahora.AddMinutes(30));
        estado.Motivo.Should().Be("Backup");
    }

    [Fact]
    public void Limpiar_ConMantenimiento_DisparaCambioYSinMantenimientoNoHaceNada()
    {
        var estado = new EstadoMantenimiento(new RelojFijo(Ahora));
        var cambios = 0;
        estado.Cambio += () => cambios++;

        estado.Limpiar();
        cambios.Should().Be(0);

        estado.Establecer(Ahora.AddMinutes(30), null);
        estado.Limpiar();

        cambios.Should().Be(2);
        estado.HayMantenimientoProgramado.Should().BeFalse();
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(0, true)]
    [InlineData(-5, true)]
    public void CobrosBloqueados_SegunLosMinutosQueFaltan_BloqueaDesdeElMinutoPrevio(int minutosQueFaltan, bool bloqueado)
    {
        var estado = new EstadoMantenimiento(new RelojFijo(Ahora));

        estado.Establecer(Ahora.AddMinutes(minutosQueFaltan), null);

        estado.CobrosBloqueados.Should().Be(bloqueado);
    }

    [Fact]
    public void CobrosBloqueados_SinMantenimiento_EsFalso()
    {
        new EstadoMantenimiento(new RelojFijo(Ahora)).CobrosBloqueados.Should().BeFalse();
    }
}
