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
        return !string.IsNullOrWhiteSpace(valor) && EsFormaCanonicaDeEmailValida(NormalizarEmail(valor));
    }

    public static string? ExigirEmailOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        // Se normaliza una sola vez: el mismo resultado se valida y se devuelve.
        var normalizado = NormalizarEmail(valor);
        if (!EsFormaCanonicaDeEmailValida(normalizado))
        {
            throw new DomainException(
                $"El email '{valor}' no es válido: debe tener la forma usuario@dominio.ext y no superar " +
                $"{LongitudMaximaEmail} caracteres.");
        }

        return normalizado;
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
        return !string.IsNullOrWhiteSpace(valor) && EsFormaCanonicaDeTelefonoValida(NormalizarTelefono(valor));
    }

    public static string? ExigirTelefonoOpcional(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = NormalizarTelefono(valor);
        if (!EsFormaCanonicaDeTelefonoValida(normalizado))
        {
            throw new DomainException(
                $"El teléfono '{valor}' no es válido: debe contener entre {DigitosMinimosTelefono} y " +
                $"{DigitosMaximosTelefono} dígitos, con un '+' inicial opcional.");
        }

        return normalizado;
    }

    // Los EsFormaCanonica* validan un texto que YA está normalizado. Los usan tanto los EsValido públicos como los
    // Exigir, para que cada valor se normalice una sola vez por llamada.
    private static bool EsFormaCanonicaDeEmailValida(string normalizado)
    {
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

    private static bool EsFormaCanonicaDeTelefonoValida(string normalizado)
    {
        var cantidadDigitos = normalizado.TrimStart('+').Length;
        return FormatoTelefono().IsMatch(normalizado)
            && cantidadDigitos >= DigitosMinimosTelefono
            && cantidadDigitos <= DigitosMaximosTelefono;
    }
}
