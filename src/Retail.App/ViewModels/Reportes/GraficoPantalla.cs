using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Retail.App.ViewModels.Reportes;

/// <summary>Series y ejes de un gráfico de LiveCharts, listos para enlazar a un <c>CartesianChart</c>.</summary>
/// <param name="Series">Series a dibujar. Los gráficos del tablero tienen una sola, así que no llevan leyenda.</param>
/// <param name="EjesX">Eje horizontal.</param>
/// <param name="EjesY">Eje vertical.</param>
public sealed record GraficoPantalla(ISeries[] Series, Axis[] EjesX, Axis[] EjesY)
{
    /// <summary>Gráfico sin datos: no dibuja nada.</summary>
    public static GraficoPantalla Vacio { get; } = new([], [], []);

    public bool TieneDatos => Series.Length > 0;
}
