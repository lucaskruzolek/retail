using FluentAssertions;
using Retail.App.Services;
using Xunit;

namespace Retail.App.UnitTests.Services;

/// <summary>
/// Instancia única por PC (MT-2). Cada prueba usa nombres aleatorios para no chocar con la aplicación real ni entre sí.
/// Un mutex es reentrante en el hilo que lo tiene: la "segunda instancia" se prueba siempre desde otro hilo, como
/// ocurre con otro proceso real.
/// </summary>
public class InstanciaUnicaTests
{
    private readonly string _nombreMutex;
    private readonly string _nombreEvento;

    public InstanciaUnicaTests()
    {
        var id = Guid.NewGuid().ToString("N");
        _nombreMutex = $@"Local\Retail.Pruebas.{id}";
        _nombreEvento = $@"Local\Retail.Pruebas.Activar.{id}";
    }

    private InstanciaUnica Crear()
    {
        return new InstanciaUnica(_nombreMutex, _nombreEvento);
    }

    private bool SegundaInstanciaAdquiere()
    {
        return Task.Run(() =>
        {
            using var otra = Crear();
            return otra.TryAdquirir();
        }).GetAwaiter().GetResult();
    }

    private bool SegundaInstanciaSenala()
    {
        return Task.Run(() =>
        {
            using var otra = Crear();
            return otra.SenalarInstanciaExistente();
        }).GetAwaiter().GetResult();
    }

    [Fact]
    public void TryAdquirir_PrimeraInstancia_Adquiere()
    {
        using var primera = Crear();

        primera.TryAdquirir().Should().BeTrue();
    }

    [Fact]
    public void TryAdquirir_SegundaInstanciaMientrasLaPrimeraLaTiene_NoAdquiere()
    {
        using var primera = Crear();
        primera.TryAdquirir().Should().BeTrue();

        SegundaInstanciaAdquiere().Should().BeFalse();
    }

    [Fact]
    public void TryAdquirir_DespuesDeDescartarLaPrimera_UnaNuevaAdquiere()
    {
        var primera = Crear();
        primera.TryAdquirir().Should().BeTrue();
        primera.Dispose();

        SegundaInstanciaAdquiere().Should().BeTrue();
    }

    [Fact]
    public void SenalarInstanciaExistente_SinInstanciaEscuchando_DevuelveFalse()
    {
        using var instancia = Crear();

        instancia.SenalarInstanciaExistente().Should().BeFalse();
    }

    [Fact]
    public void SenalarInstanciaExistente_ConLaPrimeraEscuchando_EjecutaElCallbackDeActivacion()
    {
        using var primera = Crear();
        primera.TryAdquirir().Should().BeTrue();
        using var activada = new ManualResetEventSlim(false);
        primera.EscucharActivacion(() => activada.Set());

        var senalada = SegundaInstanciaSenala();

        senalada.Should().BeTrue();
        activada.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("la primera instancia debe recibir la señal de activación");
    }
}
