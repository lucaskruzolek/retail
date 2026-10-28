namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Varios pares asignados en una sola operación (Track 6.6, Fase 5): aceptar o descartar propuestas (M-49). Se guardan
/// todos o ninguno.
/// </summary>
public record class AsignarParesDto
{
    public required IReadOnlyList<AsignarParDto> Pares { get; init; }
}
