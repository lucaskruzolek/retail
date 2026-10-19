using System.Globalization;
using System.Windows.Data;

namespace Retail.App.Converters;

/// <summary>
/// Enlaza un grupo de <c>RadioButton</c> a una propiedad de tipo enum: cada botón está marcado cuando el valor de la
/// propiedad es igual a su <c>ConverterParameter</c>, y al marcarlo escribe ese valor en la propiedad. Desmarcarse
/// (porque otro botón del grupo se marcó) no escribe nada.
/// </summary>
public class IgualAParametroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Equals(value, parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? parameter : Binding.DoNothing;
    }
}
