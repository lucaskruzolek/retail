using Retail.Domain.Exceptions;

namespace Retail.Domain.Common;

/// <summary>
/// Reglas de los códigos de barras GS1 (EAN-13, EAN-8, UPC-A y GTIN-14) y del prefijo del titular, que el motor de
/// catalogación usa para sugerir la marca (Track 6.6, M-28). La forma canónica de un código es el GTIN-13, o el EAN-8
/// cuando el código tiene 8 dígitos: "036000291452" (UPC-A) y "0036000291452" son el mismo producto.
/// </summary>
/// <remarks>
/// Reproduce la normalización de <c>ml/scripts/comun.py</c> (<c>normalizar_ean</c>): las métricas del Track 6.6 se
/// midieron con ella, y C# y Python deben tratar igual cada código.
/// </remarks>
public static class ReglasCodigoBarras
{
    /// <summary>
    /// Largo del prefijo del titular con que se arma el diccionario de prefijos (sección 6 del Track 6.6). GS1 asigna
    /// prefijos de largo variable; 7 dígitos es el largo que mejor separó a los titulares en la lista del arranque.
    /// </summary>
    public const int LongitudPrefijoTitular = 7;

    /// <summary>
    /// Largo máximo del código de un artículo (columna <c>codigo_barras</c>).
    /// </summary>
    public const int LongitudMaximaCodigoArticulo = 50;

    /// <summary>
    /// El código de un artículo es su código de barras de fábrica o un código interno corto que asigna el comercio, p.
    /// ej. "11" para fotocopias, que el POS encuentra por coincidencia exacta (M11). En los dos casos son solo dígitos,
    /// hasta <see cref="LongitudMaximaCodigoArticulo"/>. A propósito no se exige un largo GS1 ni el dígito
    /// verificador: un código interno no los tiene, y un código de proveedor mal armado igual identifica al producto.
    /// </summary>
    public static bool EsCodigoDeArticuloValido(string? codigo)
    {
        var normalizado = codigo?.Trim() ?? string.Empty;
        return normalizado.Length is > 0 and <= LongitudMaximaCodigoArticulo && normalizado.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// Devuelve el código sin espacios alrededor, o <c>null</c> si está vacío (el código es opcional, RF-04). Lanza
    /// <see cref="DomainException"/> si no es válido según <see cref="EsCodigoDeArticuloValido"/>.
    /// </summary>
    public static string? ExigirCodigoDeArticulo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var normalizado = codigo.Trim();
        if (!EsCodigoDeArticuloValido(normalizado))
        {
            throw new DomainException(
                $"El código '{normalizado}' no es válido: solo admite dígitos (código de barras o código interno), " +
                $"hasta {LongitudMaximaCodigoArticulo}.");
        }

        return normalizado;
    }

    /// <summary>
    /// Devuelve el código en su forma canónica (GTIN-13 o EAN-8), o <c>null</c> si no es un código GS1 utilizable:
    /// no numérico, de un largo que no corresponde, con el dígito verificador incorrecto o de circulación restringida
    /// (GTIN-13 que empieza con 2, que cada comercio usa para sus códigos internos o de peso variable).
    /// </summary>
    public static string? NormalizarGtin(string? codigo)
    {
        var digitos = codigo?.Trim() ?? string.Empty;
        if (digitos.Length == 0 || !digitos.All(char.IsAsciiDigit))
        {
            return null;
        }

        switch (digitos.Length)
        {
            case 11:
                // UPC-A al que Excel le quitó el cero inicial; con el cero queda como el caso de 12 dígitos.
                digitos = "00" + digitos;
                break;
            case 12:
                // UPC-A: es un GTIN-13 que empieza con cero.
                digitos = "0" + digitos;
                break;
            case 14:
                // GTIN-14: el indicador de empaque (primer dígito) entra en el cálculo del verificador, así que se
                // valida con los 14 dígitos y el GTIN-13 del producto se arma con el cuerpo y un verificador nuevo.
                if (!TieneDigitoVerificadorValido(digitos))
                {
                    return null;
                }

                var cuerpo = digitos.Substring(1, 12);
                digitos = cuerpo + CalcularDigitoVerificador(cuerpo);
                break;
        }

        if (digitos.Length is not (8 or 13) || !TieneDigitoVerificadorValido(digitos))
        {
            return null;
        }

        return digitos.Length == 13 && digitos[0] == '2' ? null : digitos;
    }

    /// <summary>
    /// Prefijo del titular de un código ya normalizado con <see cref="NormalizarGtin"/>. Un EAN-8 no lo tiene: GS1 lo
    /// asigna producto por producto, no por empresa.
    /// </summary>
    public static string? ObtenerPrefijoTitular(string? gtinNormalizado)
    {
        return gtinNormalizado is { Length: 13 } ? gtinNormalizado[..LongitudPrefijoTitular] : null;
    }

    public static bool EsPrefijoTitularValido(string? prefijo)
    {
        return EsFormaCanonicaDePrefijoValida(prefijo?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Un prefijo tiene exactamente <see cref="LongitudPrefijoTitular"/> dígitos y no empieza con 2: un código de
    /// circulación restringida nunca se normaliza (ver <see cref="NormalizarGtin"/>), así que su prefijo jamás
    /// coincidiría con un producto.
    /// </summary>
    public static string ExigirPrefijoTitular(string? prefijo)
    {
        var normalizado = prefijo?.Trim() ?? string.Empty;
        if (!EsFormaCanonicaDePrefijoValida(normalizado))
        {
            throw new DomainException(
                $"El prefijo '{prefijo}' no es válido: debe tener exactamente {LongitudPrefijoTitular} dígitos y no " +
                "puede empezar con 2 (códigos de uso interno).");
        }

        return normalizado;
    }

    /// <summary>
    /// Algoritmo GS1 módulo 10: el último dígito es el verificador de los anteriores.
    /// </summary>
    public static bool TieneDigitoVerificadorValido(string digitos)
    {
        ArgumentNullException.ThrowIfNull(digitos);
        if (digitos.Length < 2 || !digitos.All(char.IsAsciiDigit))
        {
            return false;
        }

        return CalcularDigitoVerificador(digitos[..^1]) == digitos[^1] - '0';
    }

    // Pondera 3 y 1 alternados desde la derecha del cuerpo (sin el verificador): así el cálculo es el mismo para
    // EAN-8, GTIN-13 y GTIN-14 sin importar el largo.
    private static int CalcularDigitoVerificador(string cuerpo)
    {
        var suma = 0;
        for (var i = 0; i < cuerpo.Length; i++)
        {
            var digito = cuerpo[cuerpo.Length - 1 - i] - '0';
            suma += i % 2 == 0 ? digito * 3 : digito;
        }

        return (10 - suma % 10) % 10;
    }

    private static bool EsFormaCanonicaDePrefijoValida(string normalizado)
    {
        return normalizado.Length == LongitudPrefijoTitular
            && normalizado.All(char.IsAsciiDigit)
            && normalizado[0] != '2';
    }
}
