using System.Text.RegularExpressions;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Common;

/// <summary>
/// Reglas de normalización y validación de los documentos de identidad y fiscales (DNI, CUIT, CUIL, pasaporte).
/// La forma canónica de un documento numérico son solo sus dígitos: "20-12345678-6" y "20123456786" son el
/// mismo CUIT, y guardarlos de una única forma es lo que permite que el índice único detecte el duplicado.
/// </summary>
public static partial class ReglasDocumento
{
    // Pesos del algoritmo módulo 11 que ARCA aplica a los 10 primeros dígitos de un CUIT o CUIL.
    private static readonly int[] PesosModulo11 = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    // Prefijos de persona física (20 masculino, 27 femenino, 23 y 24 cuando el 20 o el 27 colisionan).
    private static readonly HashSet<string> PrefijosPersonaFisica = new(StringComparer.Ordinal)
    {
        "20", "23", "24", "27"
    };

    // Prefijos de persona jurídica (30, y 33 o 34 cuando el 30 colisiona).
    private static readonly HashSet<string> PrefijosPersonaJuridica = new(StringComparer.Ordinal)
    {
        "30", "33", "34"
    };

    [GeneratedRegex(@"^[0-9]{7,8}$")]
    private static partial Regex FormatoDni();

    [GeneratedRegex(@"^[0-9]{11}$")]
    private static partial Regex FormatoCuitCuil();

    [GeneratedRegex(@"^[A-Z0-9]{6,9}$")]
    private static partial Regex FormatoPasaporte();

    // Separadores con que se escriben habitualmente los documentos: "12.345.678", "20-12345678-6".
    [GeneratedRegex(@"[\s.\-]")]
    private static partial Regex SeparadoresDocumento();

    /// <summary>
    /// Un DNI, un CUIL o un pasaporte siempre identifican a una persona física; un CUIT puede pertenecer
    /// tanto a una persona como a una empresa. Define si el nombre del cliente se valida como nombre de
    /// persona o como razón social.
    /// </summary>
    public static bool IdentificaPersonaFisica(TipoDocumentoEnum tipo)
    {
        return tipo != TipoDocumentoEnum.Cuit;
    }

    public static string Normalizar(TipoDocumentoEnum tipo, string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var sinSeparadores = SeparadoresDocumento().Replace(valor, string.Empty);
        return tipo == TipoDocumentoEnum.Pasaporte ? sinSeparadores.ToUpperInvariant() : sinSeparadores;
    }

    public static bool EsValido(TipoDocumentoEnum tipo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        return tipo switch
        {
            TipoDocumentoEnum.Dni => FormatoDni().IsMatch(Normalizar(tipo, valor)),
            TipoDocumentoEnum.Cuit => EsCuitValido(valor),
            TipoDocumentoEnum.Cuil => EsCuilValido(valor),
            TipoDocumentoEnum.Pasaporte => FormatoPasaporte().IsMatch(Normalizar(tipo, valor)),
            _ => false
        };
    }

    public static string Exigir(TipoDocumentoEnum tipo, string? valor)
    {
        if (!EsValido(tipo, valor))
        {
            throw new DomainException($"El número '{valor}' no es un {DescribirFormato(tipo)}.");
        }

        return Normalizar(tipo, valor!);
    }

    /// <summary>
    /// Un CUIT admite prefijos de persona física y jurídica, y su último dígito debe coincidir con el
    /// verificador módulo 11.
    /// </summary>
    public static bool EsCuitValido(string? valor)
    {
        return EsClaveTributariaValida(valor, admitePersonaJuridica: true);
    }

    /// <summary>
    /// Un CUIL solo se asigna a personas físicas, así que no admite los prefijos 30, 33 ni 34.
    /// </summary>
    public static bool EsCuilValido(string? valor)
    {
        return EsClaveTributariaValida(valor, admitePersonaJuridica: false);
    }

    /// <summary>
    /// Mensaje que describe el formato esperado para cada tipo; lo comparten el agregado y el validador.
    /// </summary>
    public static string DescribirFormato(TipoDocumentoEnum tipo)
    {
        return tipo switch
        {
            TipoDocumentoEnum.Dni => "DNI válido: debe tener 7 u 8 dígitos",
            TipoDocumentoEnum.Cuit => "CUIT válido: debe tener 11 dígitos, un prefijo existente y un dígito verificador correcto",
            TipoDocumentoEnum.Cuil => "CUIL válido: debe tener 11 dígitos, un prefijo de persona física y un dígito verificador correcto",
            TipoDocumentoEnum.Pasaporte => "pasaporte válido: debe tener entre 6 y 9 letras o números",
            _ => "documento de un tipo reconocido"
        };
    }

    private static bool EsClaveTributariaValida(string? valor, bool admitePersonaJuridica)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var digitos = SeparadoresDocumento().Replace(valor, string.Empty);
        if (!FormatoCuitCuil().IsMatch(digitos))
        {
            return false;
        }

        var prefijo = digitos[..2];
        var prefijoValido = PrefijosPersonaFisica.Contains(prefijo)
            || (admitePersonaJuridica && PrefijosPersonaJuridica.Contains(prefijo));

        return prefijoValido && CalcularDigitoVerificador(digitos) == digitos[10] - '0';
    }

    /// <summary>
    /// Módulo 11: se suma cada uno de los 10 primeros dígitos multiplicado por su peso y se calcula
    /// 11 - (suma % 11). Un resultado de 11 equivale a 0; uno de 10 no tiene dígito posible, y por eso ARCA
    /// nunca emite esa combinación (cambia el prefijo a 23, 24 o 33). Devuelve -1 en ese caso.
    /// </summary>
    private static int CalcularDigitoVerificador(string digitos)
    {
        var suma = 0;
        for (var i = 0; i < PesosModulo11.Length; i++)
        {
            suma += (digitos[i] - '0') * PesosModulo11[i];
        }

        var resultado = 11 - (suma % 11);
        return resultado switch
        {
            11 => 0,
            10 => -1,
            _ => resultado
        };
    }
}
