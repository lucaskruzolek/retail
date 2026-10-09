using System.Globalization;
using System.Windows.Data;
using Retail.Domain.Enums;

namespace Retail.App.Converters;

/// <summary>
/// Muestra los valores de los enums del Dominio con texto legible en castellano ("Tarjeta de débito" en lugar de
/// "TarjetaDebito"). Sin conversor, WPF muestra el resultado de <c>ToString()</c>. Los textos viven en la capa de
/// presentación y no en el Dominio: cómo se muestra un valor no es una regla de negocio.
/// </summary>
public class EnumATextoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Enum valor ? ATexto(valor) : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    /// <summary>
    /// Un valor sin texto definido se muestra con su nombre: un enum nuevo nunca rompe la pantalla.
    /// </summary>
    public static string ATexto(Enum valor)
    {
        return valor switch
        {
            MedioPagoEnum.Efectivo => "Efectivo",
            MedioPagoEnum.TarjetaDebito => "Tarjeta de débito",
            MedioPagoEnum.TarjetaCredito => "Tarjeta de crédito",
            MedioPagoEnum.TransferenciaQr => "Transferencia / QR",
            MedioPagoEnum.CuentaCorriente => "Cuenta corriente",
            TipoDocumentoEnum.Dni => "DNI",
            TipoDocumentoEnum.Cuit => "CUIT",
            TipoDocumentoEnum.Cuil => "CUIL",
            TipoDocumentoEnum.Pasaporte => "Pasaporte",
            CondicionIvaEnum.ConsumidorFinal => "Consumidor final",
            CondicionIvaEnum.ResponsableInscripto => "Responsable inscripto",
            CondicionIvaEnum.Monotributo => "Monotributo",
            CondicionIvaEnum.Exento => "Exento",
            _ => valor.ToString()
        };
    }
}
