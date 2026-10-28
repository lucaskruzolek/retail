namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Prefijo EAN del titular de una marca para agregarlo o quitarlo (Track 6.6, sección 8.1, M-28).
/// </summary>
public record class PrefijoMarcaDto
{
    public required int IdMarca { get; init; }

    public required string Prefijo { get; init; }
}
