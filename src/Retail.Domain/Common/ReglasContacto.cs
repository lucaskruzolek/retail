using System.Net.Mail;
using System.Text.RegularExpressions;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Common;

/// <summary>
/// Reglas de normalización y formato de los datos de contacto (email y teléfono) de clientes y proveedores.
/// Ambos son opcionales: un texto vacío se guarda como <c>null</c> y uno con contenido debe ser válido.
/// </summary>
public static partial class ReglasContacto
{
    public const int LongitudMaximaEmail = 100;
    public const int DigitosMinimosTelefono = 8;

    // E.164, la norma internacional de numeración telefónica, fija un máximo de 15 dígitos.
    public const int DigitosMaximosTelefono = 15;

    // Un "+" inicial opcional seguido solo de dígitos, una vez quitados los separadores visuales. Se usa [0-9] y
    // no \d porque en .NET \d también acepta dígitos de otros alfabetos (por ejemplo, el "٣" arábigo).
    [GeneratedRegex(@"^\+?[0-9]+$")]
    private static partial Regex FormatoTelefono();

    // Separadores con que se suele escribir un teléfono: "(011) 4555-1234", "011.4555.1234".
    [GeneratedRegex(@"[\s\-().]")]
    private static partial Regex SeparadoresTelefono();

    /// <summary>
    /// El email se guarda en minúsculas. Técnicamente la parte local (antes de la @) distingue mayúsculas según
    /// la RFC 5321, pero ningún proveedor real lo aplica y tratarlas como distintas solo genera duplicados.
    /// </summary>
    public static string NormalizarEmail(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return valor.Trim().ToLowerInvariant();
    }

    public static bool EsEmailValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var normalizado = NormalizarEmail(valor);
        if (normalizado.Length > LongitudMaximaEmail || normalizado.Any(char.IsWhiteSpace))
        {
            return false;
        }

        // MailAddress también acepta la forma "Nombre <dir@dominio>"; exigir que la dirección coincida con el
        // texto completo deja pasar solo la dirección pura.
        if (!MailAddress.TryCreate(normalizado, out var direccion) || direccion.Address != normalizado)
        {
            return false;
        }

        // MailAddress admite dominios sin punto ("a@b"), válidos en una red interna pero no para un contacto
        // comercial; cada parte del dominio debe tener contenido ("a@b..com" no).
        var partesDominio = direccion.Host.Split('.');
        return partesDominio.Length >= 2 && partesDominio.All(parte => parte.Length > 0);
    }

    public static string? ExigirEmailOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (!EsEmailValido(valor))
        {
            throw new DomainException(
                $"El email '{valor}' no es válido: debe tener la forma usuario@dominio.ext y no superar " +
                $"{LongitudMaximaEmail} caracteres.");
        }

        return NormalizarEmail(valor);
    }

    /// <summary>
    /// Deja solo los dígitos y un "+" inicial: "(011) 4555-1234" pasa a ser "01145551234". Las letras no se
    /// quitan, para que un texto como "llamar a la tarde" sea rechazado en lugar de quedar vacío.
    /// </summary>
    public static string NormalizarTelefono(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return SeparadoresTelefono().Replace(valor.Trim(), string.Empty);
    }

    public static bool EsTelefonoValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var normalizado = NormalizarTelefono(valor);
        var cantidadDigitos = normalizado.TrimStart('+').Length;
        return FormatoTelefono().IsMatch(normalizado)
            && cantidadDigitos >= DigitosMinimosTelefono
            && cantidadDigitos <= DigitosMaximosTelefono;
    }

    public static string? ExigirTelefonoOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (!EsTelefonoValido(valor))
        {
            throw new DomainException(
                $"El teléfono '{valor}' no es válido: debe contener entre {DigitosMinimosTelefono} y " +
                $"{DigitosMaximosTelefono} dígitos, con un '+' inicial opcional.");
        }

        return NormalizarTelefono(valor);
    }
}
