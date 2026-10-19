using System.Windows.Media;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Retail.App.ViewModels.Reportes;

/// <summary>
/// Arma las series y los ejes de LiveCharts de los gráficos de la pantalla. Reproduce las decisiones del PDF (D-22,
/// D-26) para que pantalla y documento se vean igual: una sola serie en carmín, líneas sin suavizar, barras
/// horizontales con el primer elemento arriba, ejes de valores desde cero y números en es-AR.
/// </summary>
public static class ConstructorGraficos
{
    /// <summary>
    /// Máximo de barras del gráfico del tablero (170 px de alto): con más, los nombres se encimarían. El PDF tiene lugar
    /// para 15; la pantalla muestra menos y la grilla, todas las filas.
    /// </summary>
    public const int MaximoBarrasTablero = 5;

    /// <summary>Máximo de barras de los gráficos de los reportes, que ocupan el 30 % del ancho.</summary>
    public const int MaximoBarrasReporte = 8;

    /// <summary>Largo máximo de una etiqueta de barra del tablero, con el "…" incluido.</summary>
    public const int LargoMaximoEtiqueta = 24;

    /// <summary>
    /// Largo máximo de una etiqueta de barra en los gráficos de los reportes (30 % del ancho): con más, las etiquetas
    /// dejan sin lugar al área de barras y los números del eje se encíman.
    /// </summary>
    public const int LargoEtiquetaReporte = 16;

    /// <summary>Máximo de etiquetas del eje horizontal de la línea del tablero; los puntos siguen siendo todos.</summary>
    public const int MaximoEtiquetasLinea = 6;

    // Cantidad de separadores que se busca en un eje de valores: en barras horizontales el eje es el ancho del gráfico
    // (30 % de la pantalla) y un importe compacto como "25 M" ocupa unos 30 px, así que se piden menos para que las etiquetas no se encimen.
    private const int SeparadoresEnLineas = 4;
    private const int SeparadoresEnBarras = 2;

    // Tamaño de las etiquetas de los ejes: más chico que el predeterminado para que entren en un gráfico angosto.
    private const double TamanoTexto = 12;

    // Múltiplos "redondos" de una potencia de diez para el paso de un eje.
    private static readonly double[] s_multiplos = [1d, 2d, 2.5d, 5d, 10d];

    // Respaldo para cuando no hay recursos de aplicación (pruebas): los mismos valores de Styles/Colors.xaml.
    private static readonly SKColor s_marcaPorDefecto = new(0x9D, 0x0F, 0x33); // PrimaryBrush
    private static readonly SKColor s_grillaPorDefecto = new(0xE4, 0xE4, 0xE7); // SurfaceBorderBrush
    private static readonly SKColor s_textoPorDefecto = new(0x52, 0x52, 0x5B); // TextSecondaryBrush

    /// <summary>Gráfico de una línea sin suavizar ni relleno, con un punto por etiqueta.</summary>
    public static GraficoPantalla Lineas(
        string nombreSerie,
        IReadOnlyList<string> etiquetas,
        IReadOnlyList<decimal> valores)
    {
        ArgumentNullException.ThrowIfNull(etiquetas);
        ArgumentNullException.ThrowIfNull(valores);

        if (valores.Count == 0)
        {
            return GraficoPantalla.Vacio;
        }

        var colores = LeerColores();
        var datos = valores.Select(v => (double)v).ToArray();

        var serie = new LineSeries<double>
        {
            Name = nombreSerie,
            Values = datos,
            Stroke = new SolidColorPaint(colores.Marca, 2),
            GeometrySize = 8,
            GeometryStroke = new SolidColorPaint(colores.Marca, 2),
            GeometryFill = new SolidColorPaint(colores.Marca),

            // Sin suavizado: una curva inventaría valores entre dos puntos (D-22).
            LineSmoothness = 0,
            Fill = null
        };

        var ejeCategorias = new Axis
        {
            // Con a lo sumo seis etiquetas entran horizontales, sin rotar. LiveCharts ignora un paso mayor a 1 en un eje de
            // etiquetas, así que se muestra una de cada ceil(n / 6) (con 15 fechas: una cada tres) y las demás quedan en
            // blanco; la línea conserva todos sus puntos.
            Labels = EtiquetasEspaciadas(etiquetas, MaximoEtiquetasLinea),
            LabelsPaint = new SolidColorPaint(colores.Texto),
            TextSize = TamanoTexto,
            LabelsRotation = 0,
            MinStep = 1,
            ForceStepToMin = true
        };

        return new GraficoPantalla([serie], [ejeCategorias], [CrearEjeValores(datos, colores, esBarra: false)]);
    }

    /// <summary>
    /// Gráfico de barras horizontales con el primer elemento arriba. LiveCharts dibuja el índice 0 abajo, así que se
    /// invierte el orden de etiquetas y valores. Toma como máximo <paramref name="maximoBarras"/> elementos.
    /// </summary>
    public static GraficoPantalla BarrasHorizontales(
        string nombreSerie,
        IReadOnlyList<string> etiquetas,
        IReadOnlyList<decimal> valores,
        int maximoBarras,
        int largoEtiqueta = LargoMaximoEtiqueta)
    {
        ArgumentNullException.ThrowIfNull(etiquetas);
        ArgumentNullException.ThrowIfNull(valores);

        var cantidad = Math.Min(Math.Min(etiquetas.Count, valores.Count), maximoBarras);
        if (cantidad == 0)
        {
            return GraficoPantalla.Vacio;
        }

        var colores = LeerColores();
        var datos = valores.Take(cantidad).Select(v => (double)v).ToArray();

        var serie = new RowSeries<double>
        {
            Name = nombreSerie,
            Values = datos.Reverse().ToArray(),
            Fill = new SolidColorPaint(colores.Marca)
        };

        var ejeCategorias = new Axis
        {
            Labels = etiquetas.Take(cantidad).Reverse().Select(e => Truncar(e, largoEtiqueta)).ToArray(),
            LabelsPaint = new SolidColorPaint(colores.Texto),
            TextSize = TamanoTexto,
            MinStep = 1,
            ForceStepToMin = true
        };

        return new GraficoPantalla([serie], [CrearEjeValores(datos, colores, esBarra: true)], [ejeCategorias]);
    }

    /// <summary>Deja una etiqueta de cada <see cref="PasoEtiquetas"/> y vacía las demás, sin cambiar la cantidad de puntos.</summary>
    public static string[] EtiquetasEspaciadas(IReadOnlyList<string> etiquetas, int maximoEtiquetas = MaximoEtiquetasLinea)
    {
        ArgumentNullException.ThrowIfNull(etiquetas);

        var paso = PasoEtiquetas(etiquetas.Count, maximoEtiquetas);
        return etiquetas.Select((etiqueta, indice) => indice % paso == 0 ? etiqueta : string.Empty).ToArray();
    }

    /// <summary>Paso entre etiquetas del eje de categorías de una línea: ceil(n / máximo), nunca menos de 1.</summary>
    public static int PasoEtiquetas(int cantidad, int maximoEtiquetas = MaximoEtiquetasLinea)
    {
        return Math.Max(1, (int)Math.Ceiling(cantidad / (double)maximoEtiquetas));
    }

    /// <summary>
    /// Corta por el medio una etiqueta larga conservando el final: en una librería los artículos de una misma línea
    /// difieren al final del nombre ("…tomo 2" / "…tomo 16").
    /// </summary>
    public static string Truncar(string etiqueta, int largoMaximo = LargoMaximoEtiqueta)
    {
        ArgumentNullException.ThrowIfNull(etiqueta);

        if (etiqueta.Length <= largoMaximo)
        {
            return etiqueta;
        }

        // Dos quintos del lugar para el final del nombre, el resto para el principio y un carácter para el "…".
        var caracteresFinales = (largoMaximo - 1) * 2 / 5;
        var caracteresIniciales = largoMaximo - 1 - caracteresFinales;
        var inicio = etiqueta[..caracteresIniciales].TrimEnd();
        var fin = etiqueta[^caracteresFinales..].TrimStart();

        return $"{inicio}…{fin}";
    }

    /// <summary>
    /// Eje de valores. Arranca en cero si no hay negativos (en una barra la longitud es el dato, D-22); con negativos el
    /// mínimo baja un 10 % para que el cero quede dentro del rango, y si todo es negativo el máximo se fija en cero. El
    /// paso entre separadores es "redondo" y deja unos cinco, para que las etiquetas no se pisen.
    /// </summary>
    private static Axis CrearEjeValores(double[] datos, ColoresGrafico colores, bool esBarra)
    {
        var minimo = datos.Length == 0 ? 0 : datos.Min();
        var maximo = datos.Length == 0 ? 0 : datos.Max();
        var maximoAbsoluto = datos.Select(Math.Abs).DefaultIfEmpty(0).Max();

        // Los decimales solo se justifican si algún dato los tiene: un eje de unidades no puede mostrar "2,00".
        var soloEnteros = datos.All(v => v == Math.Floor(v));

        var eje = new Axis
        {
            LabelsPaint = new SolidColorPaint(colores.Texto),
            TextSize = TamanoTexto,
            SeparatorsPaint = new SolidColorPaint(colores.Grilla, 1),
            Labeler = valor => FormatoReporte.EtiquetaEje(valor, maximoAbsoluto, soloEnteros)
        };

        var limiteInferior = minimo;
        var limiteSuperior = maximo;

        if (minimo >= 0 || esBarra)
        {
            limiteInferior = minimo < 0 ? minimo * 1.1 : 0;
            eje.MinLimit = limiteInferior;
        }

        if (esBarra && maximo <= 0)
        {
            limiteSuperior = 0;
            eje.MaxLimit = 0;
        }

        var rango = limiteSuperior - limiteInferior;
        if (rango > 0)
        {
            eje.MinStep = PasoRedondo(rango / (esBarra ? SeparadoresEnBarras : SeparadoresEnLineas), soloEnteros);
            eje.ForceStepToMin = true;
        }

        return eje;
    }

    /// <summary>
    /// Menor paso "redondo" (1, 2, 2,5 o 5 por una potencia de diez) que sea al menos <paramref name="pasoCrudo"/>. Con
    /// datos enteros nunca baja de 1, para que no haya dos separadores con la misma etiqueta.
    /// </summary>
    public static double PasoRedondo(double pasoCrudo, bool soloEnteros)
    {
        if (pasoCrudo <= 0 || double.IsNaN(pasoCrudo) || double.IsInfinity(pasoCrudo))
        {
            return 1;
        }

        var potencia = Math.Pow(10, Math.Floor(Math.Log10(pasoCrudo)));
        var paso = s_multiplos
            .Select(multiplo => multiplo * potencia)
            .First(candidato => candidato >= pasoCrudo * (1 - 1e-9));

        return soloEnteros ? Math.Max(paso, 1) : paso;
    }

    private static ColoresGrafico LeerColores()
    {
        return new ColoresGrafico(
            LeerRecurso("PrimaryBrush", s_marcaPorDefecto),
            LeerRecurso("SurfaceBorderBrush", s_grillaPorDefecto),
            LeerRecurso("TextSecondaryBrush", s_textoPorDefecto));
    }

    /// <summary>
    /// Lee el color de un token del sistema de diseño (Styles/Colors.xaml) para que el gráfico siga la paleta. Sin
    /// recursos de aplicación, o desde un hilo que no es el de UI, devuelve el respaldo.
    /// </summary>
    private static SKColor LeerRecurso(string clave, SKColor respaldo)
    {
        try
        {
            if (System.Windows.Application.Current?.TryFindResource(clave) is SolidColorBrush pincel)
            {
                var color = pincel.Color;
                return new SKColor(color.R, color.G, color.B, color.A);
            }
        }
        catch (InvalidOperationException)
        {
            // Acceso desde un hilo ajeno al Dispatcher de la aplicación.
        }

        return respaldo;
    }

    private readonly record struct ColoresGrafico(SKColor Marca, SKColor Grilla, SKColor Texto);
}
