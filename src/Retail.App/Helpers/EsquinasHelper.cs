using System.Windows;
using System.Windows.Media;

namespace Retail.App.Helpers;

/// <summary>
/// Esquinas redondeadas de las grillas. <c>DataGrid</c> no tiene <c>CornerRadius</c> y su contenido (el fondo gris del
/// encabezado) se saldría de las esquinas de un borde redondeado, así que la plantilla de la grilla redondea el borde
/// con <see cref="EsquinasProperty"/> y recorta su contenido con <see cref="RadioDelRecorteProperty"/>.
/// </summary>
public static class EsquinasHelper
{
    /// <summary>Radio de las esquinas de la grilla (8, como las tarjetas). Una grilla pegada a los bordes de su tarjeta lo pone en 0.</summary>
    public static readonly DependencyProperty EsquinasProperty =
        DependencyProperty.RegisterAttached(
            "Esquinas",
            typeof(CornerRadius),
            typeof(EsquinasHelper),
            new PropertyMetadata(new CornerRadius(8)));

    /// <summary>Recorta un elemento a un rectángulo redondeado de este radio, y vuelve a recortarlo cuando cambia de tamaño. 0 quita el recorte.</summary>
    public static readonly DependencyProperty RadioDelRecorteProperty =
        DependencyProperty.RegisterAttached(
            "RadioDelRecorte",
            typeof(double),
            typeof(EsquinasHelper),
            new PropertyMetadata(0.0, AlCambiarRadioDelRecorte));

    public static void SetEsquinas(DependencyObject element, CornerRadius value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(EsquinasProperty, value);
    }

    public static CornerRadius GetEsquinas(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (CornerRadius)element.GetValue(EsquinasProperty);
    }

    public static void SetRadioDelRecorte(DependencyObject element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(RadioDelRecorteProperty, value);
    }

    public static double GetRadioDelRecorte(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (double)element.GetValue(RadioDelRecorteProperty);
    }

    private static void AlCambiarRadioDelRecorte(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement elemento)
        {
            return;
        }

        elemento.SizeChanged -= AlCambiarElTamano;
        elemento.SizeChanged += AlCambiarElTamano;
        Recortar(elemento);
    }

    private static void AlCambiarElTamano(object sender, SizeChangedEventArgs e)
    {
        Recortar((FrameworkElement)sender);
    }

    private static void Recortar(FrameworkElement elemento)
    {
        double radio = GetRadioDelRecorte(elemento);
        elemento.Clip = radio <= 0 || elemento.ActualWidth <= 0 || elemento.ActualHeight <= 0
            ? null
            : new RectangleGeometry(new Rect(0, 0, elemento.ActualWidth, elemento.ActualHeight), radio, radio);
    }
}
