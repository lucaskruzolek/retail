using System.Globalization;
using System.Text.RegularExpressions;

namespace Retail.Infrastructure.ExternalServices.Excel;

/// <summary>
/// Interpreta importes recibidos como texto (típico de planillas CSV o celdas formateadas como texto)
/// con las convenciones es-AR: punto como separador de miles y coma como separador decimal.
/// </summary>
public static partial class ParseadorPrecioTexto
{
    private const int DigitosGrupoMiles = 3;

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex FormatoNormalizado();

    /// <summary>
    /// Convierte un texto a decimal. Reglas:
    /// <list type="bullet">
    /// <item>Si aparecen ambos separadores, el último es el decimal y el otro es de miles ("1.500,50" y "1,500.50" valen 1500,50).</item>
    /// <item>Una única coma es decimal ("1,50" vale 1,5); varias comas son miles.</item>
    /// <item>Varios puntos son miles ("1.500.000").</item>
    /// <item>Un único punto con exactamente 3 dígitos después y una parte entera de 1 a 3 dígitos distinta de cero
    /// es ambiguo y se resuelve como miles según es-AR ("1.500" vale 1500); en otro caso es decimal ("1.5", "12.50", "0.500").</item>
    /// </list>
    /// </summary>
    public static bool TryParse(string? texto, out decimal valor)
    {
        valor = 0m;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string limpio = texto
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("ARS", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        bool negativo = limpio.StartsWith('-');
        if (negativo)
        {
            limpio = limpio[1..];
        }

        string? normalizado = Normalizar(limpio);
        if (normalizado is null || !FormatoNormalizado().IsMatch(normalizado))
        {
            return false;
        }

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor))
        {
            return false;
        }

        if (negativo)
        {
            valor = -valor;
        }

        return true;
    }

    /// <summary>Devuelve el texto con formato invariante (solo dígitos y un punto decimal opcional).</summary>
    private static string? Normalizar(string texto)
    {
        int ultimoPunto = texto.LastIndexOf('.');
        int ultimaComa = texto.LastIndexOf(',');
        int cantidadPuntos = texto.Count(c => c == '.');
        int cantidadComas = texto.Count(c => c == ',');

        if (ultimoPunto >= 0 && ultimaComa >= 0)
        {
            bool decimalEsComa = ultimaComa > ultimoPunto;
            char miles = decimalEsComa ? '.' : ',';
            char decimalSep = decimalEsComa ? ',' : '.';

            if ((decimalEsComa ? cantidadComas : cantidadPuntos) > 1)
            {
                return null;
            }

            return texto.Replace(miles.ToString(), string.Empty, StringComparison.Ordinal)
                        .Replace(decimalSep, '.');
        }

        if (ultimaComa >= 0)
        {
            return cantidadComas == 1
                ? texto.Replace(',', '.')
                : texto.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        if (cantidadPuntos > 1)
        {
            return texto.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        if (cantidadPuntos == 1 && EsPuntoDeMiles(texto, ultimoPunto))
        {
            return texto.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        return texto;
    }

    private static bool EsPuntoDeMiles(string texto, int posicionPunto)
    {
        string parteEntera = texto[..posicionPunto];
        string parteDecimal = texto[(posicionPunto + 1)..];

        return parteDecimal.Length == DigitosGrupoMiles
            && parteEntera.Length is >= 1 and <= 3
            && parteEntera.All(char.IsAsciiDigit)
            && parteEntera.Any(c => c != '0');
    }
}
