using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using Retail.Application.DTOs.Reportes.Exportacion;
using SkiaSharp;

namespace Retail.Infrastructure.Reportes;

/// <summary>
/// Dibuja un <see cref="GraficoReporte"/> como imagen PNG con LiveCharts2 fuera de pantalla (sin ventana ni
/// Dispatcher), para incrustarlo en el PDF. Puede invocarse desde cualquier hilo.
/// </summary>
internal static class RenderizadorGraficos
{
    internal const int Ancho = 1000;
    internal const int Alto = 450;

    public static byte[] RenderizarPng(GraficoReporte grafico)
    {
        var series = grafico.Series.Select(serie => CrearSerie(grafico.Tipo, serie)).ToArray();

        var texto = new SolidColorPaint(SKColors.Black);
        var cuadricula = new SolidColorPaint(new SKColor(220, 220, 220), 1);

        var cartesiano = new SKCartesianChart
        {
            Width = Ancho,
            Height = Alto,
            Background = SKColors.White,
            Series = series,
            XAxes =
            [
                new Axis
                {
                    Labels = grafico.Etiquetas.ToArray(),
                    LabelsPaint = texto,
                    LabelsRotation = grafico.Etiquetas.Count > 8 ? 45 : 0
                }
            ],
            YAxes =
            [
                CrearEjeY(grafico, texto, cuadricula)
            ],
            LegendPosition = grafico.Series.Count > 1 ? LegendPosition.Bottom : LegendPosition.Hidden,
            LegendTextPaint = texto
        };

        using var imagen = cartesiano.GetImage();
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return datos.ToArray();
    }

    /// <summary>
    /// Eje Y. En barras la base debe ser 0: si el eje arranca en otro valor, la altura de las barras deja de ser
    /// proporcional al dato. Con valores negativos el mínimo baja un 10 % por debajo del menor valor, para que el 0 quede
    /// dentro del rango (y, si todos son negativos, el máximo se fija en 0).
    /// </summary>
    internal static Axis CrearEjeY(GraficoReporte grafico, SolidColorPaint? texto = null, SolidColorPaint? cuadricula = null)
    {
        var eje = new Axis
        {
            LabelsPaint = texto,
            SeparatorsPaint = cuadricula
        };

        if (grafico.Tipo == TipoGraficoEnum.Barras)
        {
            var valores = grafico.Series.SelectMany(serie => serie.Valores).Select(v => (double)v).ToArray();
            var minimo = valores.Length == 0 ? 0 : valores.Min();
            var maximo = valores.Length == 0 ? 0 : valores.Max();

            eje.MinLimit = minimo < 0 ? minimo * 1.1 : 0;
            if (maximo <= 0)
            {
                eje.MaxLimit = 0;
            }
        }

        return eje;
    }

    internal static ISeries CrearSerie(TipoGraficoEnum tipo, SerieGrafico serie)
    {
        var valores = serie.Valores.Select(v => (double)v).ToArray();

        if (tipo == TipoGraficoEnum.Lineas)
        {
            return new LineSeries<double>
            {
                Name = serie.Nombre,
                Values = valores,
                GeometrySize = 8,

                // Sin suavizado: una curva inventaría valores entre dos puntos (podría superar el máximo real).
                LineSmoothness = 0,
                Fill = null
            };
        }

        return new ColumnSeries<double>
        {
            Name = serie.Nombre,
            Values = valores
        };
    }
}
