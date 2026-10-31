namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Clave del par que se da de baja (Track 6.6, Fase 5): vuelve a poder proponerse a partir de los artículos.
/// </summary>
public record class QuitarParDto
{
    public required int IdTermino { get; init; }

    public required string SegundaPalabra { get; init; }
}
