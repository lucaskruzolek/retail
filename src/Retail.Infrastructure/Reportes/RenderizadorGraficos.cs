using System.Globalization;
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

    // Infrastructure no puede leer los recursos XAML de Retail.App (Clean Architecture), así que los colores de marca
    // se duplican a propósito aquí (D-26). Si cambia un token, hay que actualizar la constante.

    /// <summary>Carmín de marca: token <c>PrimaryBrush</c> de <c>src/Retail.App/Styles/Colors.xaml</c>.</summary>
    internal const string ColorMarca = "#9D0F33";

    /// <summary>Color de la grilla: token <c>SurfaceBorderBrush</c> de <c>Colors.xaml</c>.</summary>
    internal const string ColorGrilla = "#E4E4E7";

    /// <summary>Color de las etiquetas de los ejes y de las series de contexto: token <c>TextSecondaryBrush</c> de <c>Colors.xaml</c>.</summary>
    internal const string ColorTextoSecundario = "#52525B";

    // Alto de la imagen de las barras horizontales: un margen fijo para el título del eje y un alto por barra, con un
    // mínimo para que un gráfico de una o dos barras no quede aplastado.
    private const int AltoMinimoHorizontal = 250;
    private const int AltoMargenHorizontal = 60;
    private const int AltoPorBarra = 36;

    /// <summary>
    /// Largo máximo de una etiqueta de barra horizontal, con el "…" incluido: las más largas se cortan para no comerse
    /// el ancho del gráfico. La tabla del documento conserva el nombre completo.
    /// </summary>
    internal const int LargoMaximoEtiqueta = 40;

    /// <summary>
    /// Caracteres del final que conserva una etiqueta truncada. El corte va en el medio y no al final: en una librería
    /// los artículos de una misma línea difieren al final del nombre ("…tomo 2" / "…tomo 16", "…A4 rayado" /
    /// "…A4 liso"), y cortar al final los volvía indistinguibles en el gráfico.
    /// </summary>
    internal const int CaracteresFinalesConservados = 12;

    // Con el rango de valores menor a este umbral se muestran dos decimales; con uno mayor, enteros.
    private const double RangoConDecimales = 10;

    private static readonly CultureInfo CulturaArgentina = new("es-AR");

    public static byte[] RenderizarPng(GraficoReporte grafico)
    {
        var series = grafico.Series.Select((serie, indice) => CrearSerie(grafico.Tipo, serie, indice)).ToArray();

        var texto = new SolidColorPaint(SKColor.Parse(ColorTextoSecundario));
        var cuadricula = new SolidColorPaint(SKColor.Parse(ColorGrilla), 1);
        var horizontal = grafico.Tipo == TipoGraficoEnum.BarrasHorizontales;

        var cartesiano = new SKCartesianChart
        {
            Width = Ancho,
            Height = CalcularAlto(grafico),
            Background = SKColors.White,
            Series = series,
            XAxes = [horizontal ? CrearEjeValores(grafico, texto, cuadricula) : CrearEjeCategorias(grafico, texto)],
            YAxes = [horizontal ? CrearEjeCategorias(grafico, texto) : CrearEjeValores(grafico, texto, cuadricula)],
            LegendPosition = grafico.Series.Count > 1 ? LegendPosition.Bottom : LegendPosition.Hidden,
            LegendTextPaint = texto
        };

        using var imagen = cartesiano.GetImage();
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return datos.ToArray();
    }

    /// <summary>Alto de la imagen: fijo salvo en las barras horizontales, donde crece con la cantidad de barras.</summary>
    internal static int CalcularAlto(GraficoReporte grafico)
    {
        return grafico.Tipo == TipoGraficoEnum.BarrasHorizontales
            ? Math.Max(AltoMinimoHorizontal, AltoMargenHorizontal + (AltoPorBarra * grafico.Etiquetas.Count))
            : Alto;
    }

    /// <summary>
    /// Eje de categorías (las etiquetas). En barras horizontales LiveCharts dibuja el índice 0 abajo; para que el primer
    /// elemento quede arriba se invierte el orden de las etiquetas (y el de los valores en <see cref="CrearSerie"/>).
    /// </summary>
    internal static Axis CrearEjeCategorias(GraficoReporte grafico, SolidColorPaint? texto = null)
    {
        var horizontal = grafico.Tipo == TipoGraficoEnum.BarrasHorizontales;
        var etiquetas = horizontal
            ? grafico.Etiquetas.Reverse().Select(Truncar).ToArray()
            : grafico.Etiquetas.ToArray();

        return new Axis
        {
            Labels = etiquetas,
            LabelsPaint = texto,
            LabelsRotation = !horizontal && grafico.Etiquetas.Count > 8 ? 45 : 0,

            // LiveCharts omite etiquetas cuando no entran con el paso automático: se fuerza una por categoría.
            MinStep = 1,
            ForceStepToMin = true
        };
    }

    internal static string Truncar(string etiqueta)
    {
        if (etiqueta.Length <= LargoMaximoEtiqueta)
        {
            return etiqueta;
        }

        int caracteresIniciales = LargoMaximoEtiqueta - 1 - CaracteresFinalesConservados;
        var inicio = etiqueta[..caracteresIniciales].TrimEnd();
        var fin = etiqueta[^CaracteresFinalesConservados..].TrimStart();

        return $"{inicio}…{fin}";
    }

    /// <summary>
    /// Formatea un valor del eje con separador de miles y coma decimal (es-AR): enteros ("5.000") salvo que todos los
    /// datos estén por debajo de <see cref="RangoConDecimales"/> y alguno no sea entero, donde se muestran dos decimales ("2,50").
    /// </summary>
    internal static Func<double, string> CrearLabeler(GraficoReporte grafico)
    {
        var valores = grafico.Series.SelectMany(serie => serie.Valores).ToArray();
        var maximo = valores.Select(v => Math.Abs((double)v)).DefaultIfEmpty(0).Max();

        // Los decimales solo se justifican si algún dato los tiene: un eje de unidades no puede mostrar "2,00".
        var hayDecimales = valores.Any(v => v != decimal.Truncate(v));
        var formato = hayDecimales && maximo < RangoConDecimales ? "N2" : "N0";

        return valor => valor.ToString(formato, CulturaArgentina);
    }

    /// <summary>
    /// Eje de valores (el Y en barras verticales y líneas, el X en barras horizontales). En barras la base debe ser 0: si
    /// el eje arranca en otro valor, la longitud de las barras deja de ser proporcional al dato. Con valores negativos el
    /// mínimo baja un 10 % por debajo del menor valor, para que el 0 quede dentro del rango (y, si todos son negativos,
    /// el máximo se fija en 0).
    /// </summary>
    internal static Axis CrearEjeValores(GraficoReporte grafico, SolidColorPaint? texto = null, SolidColorPaint? cuadricula = null)
    {
        var eje = new Axis
        {
            LabelsPaint = texto,
            SeparatorsPaint = cuadricula,
            Labeler = CrearLabeler(grafico)
        };

        if (grafico.Tipo is TipoGraficoEnum.Barras or TipoGraficoEnum.BarrasHorizontales)
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

    /// <param name="tipo">Estilo del gráfico.</param>
    /// <param name="serie">Datos de la serie.</param>
    /// <param name="indiceSerie">Posición de la serie: la primera lleva el carmín de marca y las demás el gris de contexto.</param>
    internal static ISeries CrearSerie(TipoGraficoEnum tipo, SerieGrafico serie, int indiceSerie = 0)
    {
        var valores = serie.Valores.Select(v => (double)v).ToArray();
        var color = SKColor.Parse(indiceSerie == 0 ? ColorMarca : ColorTextoSecundario);

        if (tipo == TipoGraficoEnum.Lineas)
        {
            return new LineSeries<double>
            {
                Name = serie.Nombre,
                Values = valores,
                Stroke = new SolidColorPaint(color, 2),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(color, 2),
                GeometryFill = new SolidColorPaint(color),

                // Sin suavizado: una curva inventaría valores entre dos puntos (podría superar el máximo real).
                LineSmoothness = 0,
                Fill = null
            };
        }

        if (tipo == TipoGraficoEnum.BarrasHorizontales)
        {
            return new RowSeries<double>
            {
                Name = serie.Nombre,

                // Orden invertido: LiveCharts dibuja el índice 0 abajo y el primer elemento debe quedar arriba.
                Values = valores.Reverse().ToArray(),
                Fill = new SolidColorPaint(color)
            };
        }

        return new ColumnSeries<double>
        {
            Name = serie.Nombre,
            Values = valores,
            Fill = new SolidColorPaint(color)
        };
    }
}
