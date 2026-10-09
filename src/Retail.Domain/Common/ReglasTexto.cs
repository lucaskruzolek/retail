using System.Text;
using System.Text.RegularExpressions;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Common;

/// <summary>
/// Reglas de normalización y formato de los textos de los datos maestros (nombres de persona, razones sociales,
/// domicilios y nombres de usuario). Es la única fuente de verdad: los agregados las aplican al crearse o
/// modificarse, y los validadores de Application reutilizan los mismos predicados para dar mensajes amigables.
/// </summary>
/// <remarks>
/// Cada predicado <c>EsXValido</c> recibe el texto tal como lo escribió el usuario y lo evalúa ya normalizado,
/// de modo que el validador de la frontera y el agregado nunca discrepan sobre un mismo valor.
/// </remarks>
public static partial class ReglasTexto
{
    public const int LongitudMinimaNombrePersona = 2;
    public const int LongitudMaximaNombrePersona = 50;
    public const int LongitudMinimaRazonSocial = 2;
    public const int LongitudMaximaRazonSocial = 150;
    public const int LongitudMinimaDomicilio = 3;
    public const int LongitudMaximaDomicilio = 200;
    public const int LongitudMinimaNombreUsuario = 3;
    public const int LongitudMaximaNombreUsuario = 50;

    // Partículas de los nombres y apellidos en castellano que se escriben en minúscula ("María de los Ángeles").
    private static readonly HashSet<string> Particulas = new(StringComparer.Ordinal)
    {
        "de", "del", "la", "las", "los", "y"
    };

    // Palabras de letras (con sus marcas diacríticas combinadas) unidas por un espacio, un apóstrofo o un guion.
    [GeneratedRegex(@"^\p{L}[\p{L}\p{M}]*(?:[ '\-]\p{L}[\p{L}\p{M}]*)*$")]
    private static partial Regex FormatoNombrePersona();

    // Letras, dígitos y la puntuación habitual de una razón social ("López & Hnos. S.R.L.", "3M Argentina S.A.").
    [GeneratedRegex(@"^[\p{L}\p{M}\p{N} .,&'\-()/ºª°]+$")]
    private static partial Regex FormatoRazonSocial();

    // Empieza con letra; los separadores (punto o guion bajo) no pueden repetirse ni quedar al final.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(?:[._][a-z0-9]+)*$")]
    private static partial Regex FormatoNombreUsuario();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    /// <summary>
    /// Forma canónica de cualquier texto libre: composición Unicode NFC (una "é" pegada desde otro programa como
    /// "e" + tilde combinada pasa a ser un único carácter), sin espacios en los extremos y con los espacios
    /// internos (incluidos tabulaciones y saltos de línea) reducidos a uno solo.
    /// </summary>
    public static string NormalizarEspacios(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var compuesto = valor.Normalize(NormalizationForm.FormC);
        return Espacios().Replace(compuesto, " ").Trim();
    }

    /// <summary>
    /// Normaliza un nombre o apellido: espacios canónicos, apóstrofo tipográfico (’) convertido al simple, y
    /// mayúscula inicial en cada palabra salvo las partículas, que van en minúscula excepto al comienzo.
    /// </summary>
    public static string NormalizarNombreDePersona(string valor)
    {
        var texto = NormalizarEspacios(valor).Replace('’', '\'').ToLowerInvariant();
        var palabras = texto.Split(' ');

        for (var i = 0; i < palabras.Length; i++)
        {
            var esParticula = i > 0 && Particulas.Contains(palabras[i]);
            palabras[i] = esParticula ? palabras[i] : CapitalizarSegmentos(palabras[i]);
        }

        return string.Join(' ', palabras);
    }

    public static bool EsNombreDePersonaValido(string? valor)
    {
        return EsNombreDePersonaValido(valor, LongitudMaximaNombrePersona);
    }

    /// <summary>
    /// Valida un nombre de persona con una longitud máxima propia: el nombre completo de un cliente
    /// ("Apellido y Nombre" en un único campo, como lo pide el comprobante fiscal) admite más caracteres
    /// que el nombre o el apellido sueltos de un operador.
    /// </summary>
    public static bool EsNombreDePersonaValido(string? valor, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var normalizado = NormalizarNombreDePersona(valor);
        return normalizado.Length >= LongitudMinimaNombrePersona
            && normalizado.Length <= longitudMaxima
            && FormatoNombrePersona().IsMatch(normalizado);
    }

    public static string ExigirNombreDePersona(string? valor, string nombreCampo)
    {
        return ExigirNombreDePersona(valor, nombreCampo, LongitudMaximaNombrePersona);
    }

    public static string ExigirNombreDePersona(string? valor, string nombreCampo, int longitudMaxima)
    {
        if (!EsNombreDePersonaValido(valor, longitudMaxima))
        {
            throw new DomainException(
                $"El {nombreCampo} '{valor}' no es válido: solo admite letras, espacios, apóstrofos o guiones, " +
                $"entre {LongitudMinimaNombrePersona} y {longitudMaxima} caracteres.");
        }

        return NormalizarNombreDePersona(valor!);
    }

    /// <summary>
    /// Normaliza una razón social respetando sus mayúsculas: siglas como "S.R.L." o marcas como "3M" se
    /// escriben así a propósito y capitalizarlas las alteraría.
    /// </summary>
    public static string NormalizarRazonSocial(string valor)
    {
        return NormalizarEspacios(valor).Replace('’', '\'');
    }

    public static bool EsRazonSocialValida(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var normalizado = NormalizarRazonSocial(valor);
        return normalizado.Length >= LongitudMinimaRazonSocial
            && normalizado.Length <= LongitudMaximaRazonSocial
            && FormatoRazonSocial().IsMatch(normalizado)
            && normalizado.Any(char.IsLetter);
    }

    public static string ExigirRazonSocial(string? valor, string nombreCampo)
    {
        if (!EsRazonSocialValida(valor))
        {
            throw new DomainException(
                $"La {nombreCampo} '{valor}' no es válida: debe contener al menos una letra, solo admite letras, " +
                $"números y la puntuación . , & ' - ( ) / º ª °, entre {LongitudMinimaRazonSocial} y " +
                $"{LongitudMaximaRazonSocial} caracteres.");
        }

        return NormalizarRazonSocial(valor!);
    }

    public static string NormalizarDomicilio(string valor)
    {
        return NormalizarEspacios(valor);
    }

    public static bool EsDomicilioValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var normalizado = NormalizarDomicilio(valor);
        return normalizado.Length >= LongitudMinimaDomicilio
            && normalizado.Length <= LongitudMaximaDomicilio
            && normalizado.Any(char.IsLetter);
    }

    /// <summary>
    /// El domicilio es opcional: un texto vacío se guarda como <c>null</c> y uno con contenido debe ser válido.
    /// </summary>
    public static string? ExigirDomicilioOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (!EsDomicilioValido(valor))
        {
            throw new DomainException(
                $"El domicilio '{valor}' no es válido: debe contener al menos una letra, entre " +
                $"{LongitudMinimaDomicilio} y {LongitudMaximaDomicilio} caracteres.");
        }

        return NormalizarDomicilio(valor);
    }

    /// <summary>
    /// El nombre de usuario se guarda en minúsculas para que la unicidad y el login no dependan de la
    /// collation del servidor: "JPerez" y "jperez" son la misma cuenta por regla de dominio, no por casualidad.
    /// </summary>
    public static string NormalizarNombreUsuario(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return valor.Trim().ToLowerInvariant();
    }

    public static bool EsNombreUsuarioValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var normalizado = NormalizarNombreUsuario(valor);
        return normalizado.Length >= LongitudMinimaNombreUsuario
            && normalizado.Length <= LongitudMaximaNombreUsuario
            && FormatoNombreUsuario().IsMatch(normalizado);
    }

    public static string ExigirNombreUsuario(string? valor)
    {
        if (!EsNombreUsuarioValido(valor))
        {
            throw new DomainException(
                $"El nombre de usuario '{valor}' no es válido: debe empezar con una letra y solo admite letras sin " +
                $"tilde, números, puntos o guiones bajos no consecutivos, entre {LongitudMinimaNombreUsuario} y " +
                $"{LongitudMaximaNombreUsuario} caracteres.");
        }

        return NormalizarNombreUsuario(valor!);
    }

    // "maría-josé" -> "María-José", "o'connor" -> "O'Connor": mayúscula al inicio y después de cada guion o apóstrofo.
    private static string CapitalizarSegmentos(string palabra)
    {
        var caracteres = palabra.ToCharArray();
        var inicioDeSegmento = true;

        for (var i = 0; i < caracteres.Length; i++)
        {
            if (inicioDeSegmento && char.IsLetter(caracteres[i]))
            {
                caracteres[i] = char.ToUpperInvariant(caracteres[i]);
                inicioDeSegmento = false;
            }
            else if (caracteres[i] is '-' or '\'')
            {
                inicioDeSegmento = true;
            }
        }

        return new string(caracteres);
    }
}
