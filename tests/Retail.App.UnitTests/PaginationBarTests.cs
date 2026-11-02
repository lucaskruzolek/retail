using FluentAssertions;
using Retail.App.Views.Components;
using Xunit;

namespace Retail.App.UnitTests;

/// <summary>
/// <see cref="PaginationBar.CalcularElementos"/>: qué números de página se muestran y dónde van los "…". Se usa
/// <c>0</c> en las expectativas para representar un salto.
/// </summary>
public class PaginationBarTests
{
    private static int[] Lugares(int actual, int total)
    {
        return PaginationBar.CalcularElementos(actual, total).Select(e => e.Numero ?? 0).ToArray();
    }

    [Fact]
    public void CalcularElementos_PocasPaginas_LasMuestraTodasSinSaltos()
    {
        Lugares(2, 5).Should().Equal(1, 2, 3, 4, 5);
        Lugares(1, 7).Should().Equal(1, 2, 3, 4, 5, 6, 7);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(10)]
    public void CalcularElementos_CercaDeUnExtremo_MuestraLosTresPrimerosYLosTresUltimos(int actual)
    {
        Lugares(actual, 10).Should().Equal(1, 2, 3, 0, 8, 9, 10);
    }

    [Fact]
    public void CalcularElementos_EnElMedio_MuestraLaActualConSusVecinasEntreSaltos()
    {
        Lugares(5, 10).Should().Equal(1, 0, 4, 5, 6, 0, 10);
    }

    [Fact]
    public void CalcularElementos_SiempreSieteLugaresConMuchasPaginas_LaBarraNoCambiaDeAncho()
    {
        foreach (var actual in Enumerable.Range(1, 40))
        {
            PaginationBar.CalcularElementos(actual, 40).Should().HaveCount(7);
        }
    }

    [Fact]
    public void CalcularElementos_MarcaComoActualSoloLaPaginaActual()
    {
        PaginationBar.CalcularElementos(5, 10).Where(e => e.EsActual).Select(e => e.Numero).Should().Equal(5);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 3)]
    public void CalcularElementos_ValoresFueraDeRango_SeAjustanAlRango(int actual, int total)
    {
        var elementos = PaginationBar.CalcularElementos(actual, total);

        elementos.Should().ContainSingle(e => e.EsActual);
        elementos.Should().OnlyContain(e => e.Numero == null || (e.Numero >= 1 && e.Numero <= Math.Max(1, total)));
    }
}
