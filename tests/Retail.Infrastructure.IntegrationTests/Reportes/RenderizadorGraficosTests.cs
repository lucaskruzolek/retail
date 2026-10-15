using FluentAssertions;
using LiveChartsCore.SkiaSharpView;
using Retail.Application.DTOs.Reportes.Exportacion;
using Retail.Infrastructure.Reportes;
using Xunit;

namespace Retail.Infrastructure.IntegrationTests.Reportes;

public class RenderizadorGraficosTests
{
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47];

    [Theory]
    [InlineData(TipoGraficoEnum.Barras)]
    [InlineData(TipoGraficoEnum.Lineas)]
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
    public void CrearEjeY_BarrasConValoresPositivos_ArrancaEnCero()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a", "b"], [new SerieGrafico("s", [100m, 250m])]);

        RenderizadorGraficos.CrearEjeY(grafico).MinLimit.Should().Be(0);
    }

    [Fact]
    public void CrearEjeY_BarrasConNegativo_DejaElCeroDentroDelRango()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a", "b"], [new SerieGrafico("s", [-20m, 50m])]);

        var eje = RenderizadorGraficos.CrearEjeY(grafico);

        eje.MinLimit.Should().BeLessThan(-20);
        eje.MaxLimit.Should().BeNull();
    }

    [Fact]
    public void CrearEjeY_BarrasSoloNegativas_FijaElMaximoEnCero()
    {
        var grafico = new GraficoReporte("g", TipoGraficoEnum.Barras, ["a"], [new SerieGrafico("s", [-20m])]);

        RenderizadorGraficos.CrearEjeY(grafico).MaxLimit.Should().Be(0);
    }

    [Fact]
    public void CrearSerie_Lineas_NoSuavizaLaCurva()
    {
        var serie = RenderizadorGraficos.CrearSerie(TipoGraficoEnum.Lineas, new SerieGrafico("s", [1m, 3m]));

        serie.Should().BeOfType<LineSeries<double>>().Which.LineSmoothness.Should().Be(0);
    }
}
