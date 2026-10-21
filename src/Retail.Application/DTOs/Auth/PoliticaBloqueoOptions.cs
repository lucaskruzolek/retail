namespace Retail.Application.DTOs.Auth;

/// <summary>
/// Parámetros configurables de la política de intentos fallidos y bloqueo temporal de cuentas.
/// </summary>
public class PoliticaBloqueoOptions
{
    public const string SectionName = "PoliticaBloqueo";

    public int MaxIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 1;
}
