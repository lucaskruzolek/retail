using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Common;

/// <summary>
/// Reglas de los diccionarios del motor de catalogación (Track 6.6): términos y alias de marca. Se guardan en su
/// forma de comparación, en mayúsculas y sin tildes, porque se buscan en las descripciones telegráficas de los
/// proveedores ("LAPIZ", "LÁPIZ" y "lapiz" son el mismo término).
/// </summary>
/// <remarks>
/// <see cref="NormalizarTexto"/> reproduce <c>normalizar_texto</c> de <c>ml/scripts/comun.py</c>, con la que se
/// armaron los diccionarios y se midieron los experimentos. Como allí, la Ñ pierde su tilde ("AÑO" → "ANO"): es una
/// forma de comparar, no de mostrar, y las descripciones de los proveedores la escriben de las dos maneras.
/// </remarks>
public static partial class ReglasClasificacion
{
    /// <summary>
    /// Un término es la primera palabra de una descripción, que los proveedores limitan a unos 45 caracteres.
    /// </summary>
    public const int LongitudMaximaTermino = 50;

    public const int LongitudMaximaAlias = ReglasTexto.LongitudMaximaNombreClasificacion;

    /// <summary>
    /// Rubro, categoría o marca escritos por un proveedor en su planilla (Track 6.6, M-12).
    /// </summary>
    public const int LongitudMaximaTextoDeProveedor = ReglasTexto.LongitudMaximaNombreClasificacion;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    /// <summary>
    /// Forma de comparación de un texto: sin marcas diacríticas, en mayúsculas, sin espacios en los extremos y con
    /// los espacios internos reducidos a uno solo.
    /// </summary>
    public static string NormalizarTexto(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var sinMarcas = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinMarcas.Append(caracter);
            }
        }

        return Espacios().Replace(sinMarcas.ToString(), " ").Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Descripción del proveedor sin los prefijos de control (<c>*</c>, <c>**</c>), con los espacios colapsados y en
    /// mayúsculas (<c>limpiar_descripcion</c> de <c>comun.py</c>). Conserva las tildes: es el texto que se muestra y del
    /// que se derivan el término y las palabras de marca.
    /// </summary>
    /// <remarks>
    /// Como en Python, solo quita los asteriscos del comienzo: "* * CUADERNO" queda "* CUADERNO" y su primera palabra
    /// es "*". No ocurre en la lista del arranque (0 de 14.876 descripciones) y se conserva por paridad.
    /// </remarks>
    public static string LimpiarDescripcion(string descripcion)
    {
        ArgumentNullException.ThrowIfNull(descripcion);
        var sinPrefijo = descripcion.Trim().TrimStart('*').Trim();
        return Espacios().Replace(sinPrefijo, " ").ToUpperInvariant();
    }

    /// <summary>
    /// Primera palabra de la descripción limpia, el sustantivo núcleo aproximado (<c>primera_palabra</c>); vacía si la
    /// descripción no tiene texto.
    /// </summary>
    public static string PrimeraPalabra(string descripcion)
    {
        var limpia = LimpiarDescripcion(descripcion);
        return limpia.Length == 0 ? string.Empty : limpia.Split(' ')[0];
    }

    /// <summary>
    /// Término de la descripción en forma de comparación: la clave con que se busca en la tabla de términos.
    /// </summary>
    public static string TerminoDeDescripcion(string descripcion)
    {
        return NormalizarTexto(PrimeraPalabra(descripcion));
    }

    /// <summary>
    /// Palabras de la descripción donde se buscan las marcas (<c>marcas_en</c>): en forma de comparación, sin la primera
    /// (es el término), sin el punto final y separadas también por la barra, porque el proveedor escribe la marca
    /// pegada a la línea ("KOBY/DATA").
    /// </summary>
    public static IReadOnlyList<string> PalabrasParaMarca(string descripcion)
    {
        var palabras = NormalizarTexto(LimpiarDescripcion(descripcion)).Split(' ');
        return palabras
            .Skip(1)
            .SelectMany(palabra => palabra.TrimEnd('.').Split('/'))
            .Where(parte => parte.Length > 0)
            .ToList();
    }

    public static bool EsTerminoValido(string? valor)
    {
        return !string.IsNullOrWhiteSpace(valor) && EsFormaCanonicaDeTerminoValida(NormalizarTexto(valor));
    }

    /// <summary>
    /// Un término es una sola palabra: el motor lo compara con la primera palabra de la descripción. Puede no tener
    /// letras ("100", en "100 SOBRES ...").
    /// </summary>
    public static string ExigirTermino(string? valor)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? string.Empty : NormalizarTexto(valor);
        if (!EsFormaCanonicaDeTerminoValida(normalizado))
        {
            throw new DomainException(
                $"El término '{valor}' no es válido: debe ser una sola palabra, sin espacios, de hasta " +
                $"{LongitudMaximaTermino} caracteres.");
        }

        return normalizado;
    }

    public static bool EsAliasValido(string? valor)
    {
        return !string.IsNullOrWhiteSpace(valor) && EsFormaCanonicaDeAliasValida(NormalizarTexto(valor));
    }

    /// <summary>
    /// Un alias puede tener varias palabras ("ARTISTICA DIBU"): el motor lo busca como frase completa.
    /// </summary>
    public static string ExigirAlias(string? valor)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? string.Empty : NormalizarTexto(valor);
        if (!EsFormaCanonicaDeAliasValida(normalizado))
        {
            throw new DomainException(
                $"El alias '{valor}' no es válido: no puede estar vacío ni superar los {LongitudMaximaAlias} " +
                "caracteres.");
        }

        return normalizado;
    }

    /// <summary>
    /// Forma de comparación de un texto de clasificación del proveedor ("Cuadernos T/D" → "CUADERNOS T/D"), que es la
    /// clave de sus equivalencias (M-31): así "Escolar" y "ESCOLAR" en dos planillas son el mismo valor.
    /// </summary>
    public static string ExigirTextoDeProveedor(string? valor, string nombreCampo)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? string.Empty : NormalizarTexto(valor);
        if (normalizado.Length == 0 || normalizado.Length > LongitudMaximaTextoDeProveedor)
        {
            throw new DomainException(
                $"El {nombreCampo} '{valor}' no es válido: no puede estar vacío ni superar los " +
                $"{LongitudMaximaTextoDeProveedor} caracteres.");
        }

        return normalizado;
    }

    // Validan un texto que YA está normalizado, como los EsFormaCanonica* de ReglasTexto.
    private static bool EsFormaCanonicaDeTerminoValida(string normalizado)
    {
        return normalizado.Length > 0
            && normalizado.Length <= LongitudMaximaTermino
            && !normalizado.Contains(' ');
    }

    private static bool EsFormaCanonicaDeAliasValida(string normalizado)
    {
        return normalizado.Length > 0 && normalizado.Length <= LongitudMaximaAlias;
    }
}
