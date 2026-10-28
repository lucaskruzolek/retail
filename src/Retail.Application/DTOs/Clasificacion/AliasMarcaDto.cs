namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Alias de una marca para agregarlo o quitarlo (Track 6.6, sección 8.1).
/// </summary>
public record class AliasMarcaDto
{
    public required int IdMarca { get; init; }

    public required string Alias { get; init; }
}
