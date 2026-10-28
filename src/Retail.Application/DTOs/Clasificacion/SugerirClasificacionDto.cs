namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Lote del catálogo de un proveedor para el que se piden sugerencias de clasificación (RF-27).
/// </summary>
public record class SugerirClasificacionDto
{
    public required int IdProveedor { get; init; }

    public required IReadOnlyList<int> IdsCatalogo { get; init; }
}
