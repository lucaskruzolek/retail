using System.Globalization;
using System.Windows.Data;

namespace Retail.App.Converters;

/// <summary>
/// Muestra un texto en mayúsculas (cultura es-AR) sin tocar el dato: la grilla de artículos presenta las descripciones
/// normalizadas aunque se hayan cargado en minúsculas. Es solo de presentación, de ahí que no tenga vuelta.
/// </summary>
public class AMayusculasConverter : IValueConverter
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string texto ? texto.ToUpper(CulturaArgentina) : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("La conversión a mayúsculas es solo de presentación.");
    }
}
