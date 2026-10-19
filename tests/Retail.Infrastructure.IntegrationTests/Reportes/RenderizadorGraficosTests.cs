using FluentAssertions;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Retail.Application.DTOs.Reportes.Exportacion;
using Retail.Infrastructure.Reportes;
using SkiaSharp;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Reportes;

public class RenderizadorGraficosTests
{
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47];

    [Theory]
    [InlineData(TipoGraficoEnum.Barras)]
    [InlineData(TipoGraficoEnum.Lineas)]
    [InlineData(TipoGraficoEnum.BarrasHorizontales)]
    public async Task RenderizarPng_DesdeHiloSinDispatcher_GeneraPngValido(TipoGraficoEnum tipo)
    {
        var grafico = DocumentoReportePrueba.Completo().Graficos.First(g => g.Tipo == tipo);

        var png = await Task.Run(() => RenderizadorGraficos.RenderizarPng(grafico));

        png.Take(4).Should().Equal(FirmaPng);
        png.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void SkiaSharp_VersionCargada_EsLaTres()
    {
        // R-8 / D-13: una sola versión de SkiaSharp (3.x) en toda la solución.
        typeof(SkiaSharp.SKImage).Assembly.GetName().Version!.Major.Should().Be(3);
    }

    [Fact]
    public void CrearEjeValores_BarrasConValoresPositivos_ArrancaEnCero()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a", "b"], [new SerieGrafico("s", [100m, 250m])]);

        RenderizadorGraficos.CrearEjeValores(grafico).MinLimit.Should().Be(0);
    }

    [Fact]
    public void CrearEjeValores_BarrasConNegativo_DejaElCeroDentroDelRango()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a", "b"], [new SerieGrafico("s", [-20m, 50m])]);

        var eje = RenderizadorGraficos.CrearEjeValores(grafico);

        eje.MinLimit.Should().BeLessThan(-20);
        eje.MaxLimit.Should().BeNull();
    }

    [Fact]
    public void CrearEjeValores_BarrasSoloNegativas_FijaElMaximoEnCero()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a"], [new SerieGrafico("s", [-20m])]);

        RenderizadorGraficos.CrearEjeValores(grafico).MaxLimit.Should().Be(0);
    }

    [Fact]
    public void CrearSerie_Lineas_NoSuavizaLaCurva()
    {
        var serie = RenderizadorGraficos.CrearSerie(TipoGraficoEnum.Lineas, new SerieGrafico("s", [1m, 3m]));

        serie.Should().BeOfType<LineSeries<double>>().Which.LineSmoothness.Should().Be(0);
    }

    private static GraficoReporte Horizontal(int cantidad, decimal primero = 10m)
    {
        var etiquetas = Enumerable.Range(1, cantidad).Select(i => $"Categoría {i}").ToList();
        var valores = Enumerable.Range(1, cantidad).Select(i => i == 1 ? primero : i * 10m).ToList();
        return new GraficoReporte("g", TipoGraficoEnum.BarrasHorizontales, etiquetas, [new SerieGrafico("s", valores)]);
    }

    private static int AltoDe(byte[] png)
    {
        using var imagen = SKBitmap.Decode(png);
        return imagen.Height;
    }

    [Theory]
    [InlineData(1, 250)]
    [InlineData(5, 250)]
    [InlineData(15, 600)]
    public void RenderizarPng_BarrasHorizontales_AltoProporcionalALaCantidad(int cantidad, int altoEsperado)
    {
        var png = RenderizadorGraficos.RenderizarPng(Horizontal(cantidad));

        AltoDe(png).Should().Be(altoEsperado);
        RenderizadorGraficos.CalcularAlto(Horizontal(cantidad)).Should().Be(altoEsperado);
    }

    [Fact]
    public void RenderizarPng_BarrasVerticales_ConservanElAltoFijo()
    {
        var grafico = DocumentoReportePrueba.Completo().Graficos.First(g => g.Tipo == TipoGraficoEnum.Barras);

        AltoDe(RenderizadorGraficos.RenderizarPng(grafico)).Should().Be(RenderizadorGraficos.Alto);
    }

    [Fact]
    public void BarrasHorizontales_ValoresYEtiquetas_SeInvierteParaQueElPrimeroQuedeArriba()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.BarrasHorizontales, ["a", "b", "c"], [new SerieGrafico("s", [1m, 2m, 3m])]);

        var serie = RenderizadorGraficos.CrearSerie(grafico.Tipo, grafico.Series[0]);
        var eje = RenderizadorGraficos.CrearEjeCategorias(grafico);

        serie.Should().BeOfType<RowSeries<double>>().Which.Values.Should().Equal(3d, 2d, 1d);
        eje.Labels.Should().Equal("c", "b", "a");
    }

    [Fact]
    public void CrearSerie_PrimeraSerie_UsaElCarminDeMarca()
    {
        var serie = RenderizadorGraficos.CrearSerie(TipoGraficoEnum.Barras, new SerieGrafico("s", [1m]));

        serie.Should().BeOfType<ColumnSeries<double>>().Which.Fill.Should().BeOfType<SolidColorPaint>()
            .Which.Color.Should().Be(SKColor.Parse("#9D0F33"));
    }

    [Fact]
    public void CrearSerie_BarrasHorizontales_UsaElCarminDeMarca()
    {
        var serie = RenderizadorGraficos.CrearSerie(TipoGraficoEnum.BarrasHorizontales, new SerieGrafico("s", [1m]));

        serie.Should().BeOfType<RowSeries<double>>().Which.Fill.Should().BeOfType<SolidColorPaint>()
            .Which.Color.Should().Be(SKColor.Parse("#9D0F33"));
    }

    [Fact]
    public void CrearSerie_SegundaSerie_UsaElGrisDeContexto()
    {
        var serie = RenderizadorGraficos.CrearSerie(TipoGraficoEnum.Barras, new SerieGrafico("s", [1m]), indiceSerie: 1);

        serie.Should().BeOfType<ColumnSeries<double>>().Which.Fill.Should().BeOfType<SolidColorPaint>()
            .Which.Color.Should().Be(SKColor.Parse("#52525B"));
    }

    [Fact]
    public void CrearSerie_Lineas_UsaCarminGrosorDosPuntosDeOchoYSinRelleno()
    {
        var serie = RenderizadorGraficos.CrearSerie(TipoGraficoEnum.Lineas, new SerieGrafico("s", [1m, 3m]));

        var linea = serie.Should().BeOfType<LineSeries<double>>().Subject;
        linea.Stroke.Should().BeOfType<SolidColorPaint>().Which.Color.Should().Be(SKColor.Parse("#9D0F33"));
        linea.Stroke.As<SolidColorPaint>().StrokeThickness.Should().Be(2);
        linea.GeometrySize.Should().Be(8);
        linea.Fill.Should().BeNull();
    }

    [Fact]
    public void CrearEjeValores_BarrasHorizontalesConPositivos_ArrancaEnCero()
    {
        RenderizadorGraficos.CrearEjeValores(Horizontal(3)).MinLimit.Should().Be(0);
    }

    [Fact]
    public void CrearEjeValores_BarrasHorizontalesConNegativo_DejaElCeroDentroDelRango()
    {
        var eje = RenderizadorGraficos.CrearEjeValores(Horizontal(3, primero: -20m));

        eje.MinLimit.Should().BeLessThan(-20);
        eje.MaxLimit.Should().BeNull();
    }

    [Fact]
    public void CrearEjeCategorias_BarrasHorizontales_ForzaUnPasoDeUno()
    {
        var eje = RenderizadorGraficos.CrearEjeCategorias(Horizontal(16));

        eje.MinStep.Should().Be(1);
        eje.ForceStepToMin.Should().BeTrue();
    }

    [Theory]
    [InlineData(39)]
    [InlineData(40)]
    public void CrearEjeCategorias_EtiquetaDeHastaCuarentaCaracteres_NoLaTrunca(int largo)
    {
        var nombre = new string('á', largo);
        var grafico = new GraficoReporte("g", TipoGraficoEnum.BarrasHorizontales, [nombre], [new SerieGrafico("s", [1m])]);

        var etiqueta = RenderizadorGraficos.CrearEjeCategorias(grafico).Labels!.Single();

        etiqueta.Should().Be(nombre);
    }

    [Fact]
    public void CrearEjeCategorias_EtiquetasLargasQueDifierenAlFinal_LasTruncaEnElMedioYSiguenSiendoDistintas()
    {
        const string tomo2 = "Diccionario enciclopédico ilustrado de la lengua castellana, tomo 2";
        const string tomo16 = "Diccionario enciclopédico ilustrado de la lengua castellana, tomo 16";
        var grafico = new GraficoReporte(
            "g",
            TipoGraficoEnum.BarrasHorizontales,
            [tomo2, tomo16],
            [new SerieGrafico("s", [2m, 1m])]);

        var etiquetas = RenderizadorGraficos.CrearEjeCategorias(grafico).Labels!;

        etiquetas.Should().OnlyContain(e => e.Length <= RenderizadorGraficos.LargoMaximoEtiqueta && e.Contains('…'));
        etiquetas.Should().OnlyHaveUniqueItems();
        etiquetas.Should().Contain(e => e.StartsWith("Diccionario enciclopédico") && e.EndsWith("tomo 16"));
        etiquetas.Should().Contain(e => e.EndsWith("tomo 2"));
    }

    [Fact]
    public void CrearEjeValores_ValoresGrandes_FormateaConSeparadorDeMilesAr()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a"], [new SerieGrafico("s", [5000m])]);

        RenderizadorGraficos.CrearEjeValores(grafico).Labeler(5000).Should().Be("5.000");
    }

    [Fact]
    public void CrearEjeValores_DatosNoEnterosYRangoMenorADiez_UsaDosDecimalesConComa()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Lineas, ["a", "b"], [new SerieGrafico("s", [1.5m, 3m])]);

        RenderizadorGraficos.CrearEjeValores(grafico).Labeler(2.5).Should().Be("2,50");
    }

    [Fact]
    public void CrearEjeValores_DatosEnterosYRangoMenorADiez_NoMuestraDecimales()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Lineas, ["a", "b", "c"], [new SerieGrafico("s", [1m, 2m, 3m])]);

        RenderizadorGraficos.CrearEjeValores(grafico).Labeler(2).Should().Be("2");
    }
}
