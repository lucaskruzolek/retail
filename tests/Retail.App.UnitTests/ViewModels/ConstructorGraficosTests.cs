using FluentAssertions;
using LiveChartsCore.SkiaSharpView;
using Retail.App.ViewModels.Reportes;
using Xunit;

namespace Retail.App.UnitTests.ViewModels;

public class ConstructorGraficosTests
{
    [Fact]
    public void Lineas_SinPuntos_DevuelveUnGraficoVacio()
    {
        // Act
        var grafico = ConstructorGraficos.Lineas("Ventas", [], []);

        // Assert
        grafico.TieneDatos.Should().BeFalse();
    }

    [Fact]
    public void BarrasHorizontales_ConValoresNegativos_DejaElCeroDentroDelRango()
    {
        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Margen", ["A", "B"], [-100m, 50m], ConstructorGraficos.MaximoBarrasReporte);

        // Assert: el mínimo baja un 10 % para que se vea la barra negativa completa
        grafico.EjesX[0].MinLimit.Should().BeApproximately(-110d, 0.0001);
    }

    [Fact]
    public void BarrasHorizontales_ConTodosLosValoresPositivos_ArrancaElEjeEnCero()
    {
        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Margen", ["A", "B"], [10m, 50m], ConstructorGraficos.MaximoBarrasReporte);

        // Assert (D-22: en una barra la longitud es el dato)
        grafico.EjesX[0].MinLimit.Should().Be(0);
    }

    [Fact]
    public void BarrasHorizontales_ConDatosGrandes_FormateaElEjeConSeparadorDeMilesArgentino()
    {
        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Margen", ["A"], [5000m], ConstructorGraficos.MaximoBarrasReporte);

        // Assert
        grafico.EjesX[0].Labeler!(5000d).Should().Be("5.000");
        grafico.Series.Should().ContainSingle().Which.Should().BeOfType<RowSeries<double>>();
    }

    [Fact]
    public void BarrasHorizontales_ConDatosEnterosPequenos_NoMuestraDecimalesEnElEje()
    {
        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Unidades", ["A", "B", "C"], [1m, 2m, 3m], ConstructorGraficos.MaximoBarrasReporte);

        // Assert
        grafico.EjesX[0].Labeler!(2d).Should().Be("2");
    }

    [Fact]
    public void BarrasHorizontales_ConDatosNoEnterosPequenos_MuestraDosDecimales()
    {
        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Margen", ["A", "B"], [1.5m, 3m], ConstructorGraficos.MaximoBarrasReporte);

        // Assert
        grafico.EjesX[0].Labeler!(2.5d).Should().Be("2,50");
    }

    [Theory]
    [InlineData(150, false, 200d)]
    [InlineData(300, false, 500d)]
    [InlineData(0.4, false, 0.5d)]
    [InlineData(1100, false, 2000d)]
    [InlineData(2.2, false, 2.5d)]
    [InlineData(0.3, true, 1d)]
    public void PasoRedondo_ElijeUnoDosDosYMedioOCincoPorUnaPotenciaDeDiez(double crudo, bool soloEnteros, double esperado)
    {
        // Act + Assert
        ConstructorGraficos.PasoRedondo(crudo, soloEnteros).Should().BeApproximately(esperado, 1e-9);
    }

    [Fact]
    public void BarrasHorizontales_ConUnRangoAncho_FijaUnPasoRedondoQueDejaPocosSeparadores()
    {
        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Margen", ["A", "B"], [800m, 1400m], ConstructorGraficos.MaximoBarrasReporte);

        // Assert: rango 0 a 1.400 y dos separadores deseados (barras horizontales): paso crudo 700 -> 1.000
        grafico.EjesX[0].MinStep.Should().Be(1000d);
        grafico.EjesX[0].ForceStepToMin.Should().BeTrue();
    }

    [Fact]
    public void Truncar_EtiquetaLarga_ConservaElFinalDelNombre()
    {
        // Arrange
        var etiqueta = "Cuaderno universitario espiral tapa dura rayado A4 tomo 16";

        // Act
        var corta = ConstructorGraficos.Truncar(etiqueta);

        // Assert
        corta.Length.Should().BeLessOrEqualTo(ConstructorGraficos.LargoMaximoEtiqueta).And.Be(24);
        corta.Should().Contain("…").And.EndWith("tomo 16");
    }

    [Fact]
    public void Lineas_ConQuinceFechas_MuestraUnaEtiquetaCadaTresSinRotarYConservaTodosLosPuntos()
    {
        // Arrange
        var etiquetas = Enumerable.Range(1, 15).Select(i => $"{i:D2}/10").ToList();
        var valores = Enumerable.Range(1, 15).Select(i => i * 1000m).ToList();

        // Act
        var grafico = ConstructorGraficos.Lineas("Ventas", etiquetas, valores);

        // Assert
        grafico.EjesX[0].LabelsRotation.Should().Be(0);
        grafico.EjesX[0].Labels.Should().HaveCount(15);
        grafico.EjesX[0].Labels!.Where(e => e.Length > 0).Should().Equal("01/10", "04/10", "07/10", "10/10", "13/10");
        ((IEnumerable<double>)((LineSeries<double>)grafico.Series[0]).Values!).Should().HaveCount(15);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(15, 3)]
    [InlineData(30, 5)]
    public void PasoEtiquetas_ElevaElPasoParaMostrarComoMuchoSeisEtiquetas(int cantidad, int esperado)
    {
        // Act + Assert
        ConstructorGraficos.PasoEtiquetas(cantidad).Should().Be(esperado);
    }

    [Fact]
    public void BarrasHorizontales_ConMasElementosQueElMaximo_RecortaAlMaximo()
    {
        // Arrange
        var etiquetas = Enumerable.Range(1, 12).Select(i => $"Grupo {i}").ToList();
        var valores = Enumerable.Range(1, 12).Select(i => (decimal)i).ToList();

        // Act
        var grafico = ConstructorGraficos.BarrasHorizontales("Margen", etiquetas, valores, ConstructorGraficos.MaximoBarrasTablero);

        // Assert
        grafico.EjesY[0].Labels.Should().Equal("Grupo 5", "Grupo 4", "Grupo 3", "Grupo 2", "Grupo 1");
    }

    [Fact]
    public void Truncar_ConUnLargoMasChico_RespetaElLargoYConservaElFinal()
    {
        // Act
        var corta = ConstructorGraficos.Truncar("Categoría escolar y de oficina número 12", ConstructorGraficos.LargoEtiquetaReporte);

        // Assert
        corta.Length.Should().BeLessOrEqualTo(16);
        corta.Should().Contain("…").And.EndWith("ro 12");
    }

    [Fact]
    public void Truncar_EtiquetaCorta_LaDejaIgual()
    {
        // Act + Assert
        ConstructorGraficos.Truncar("Libros").Should().Be("Libros");
    }
}
