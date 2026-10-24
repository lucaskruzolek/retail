namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Ítems del catálogo de un proveedor a incorporar a la tabla propia de Artículos, cada uno con su margen y su
/// clasificación (Track 6.6, R-7). La clasificación es por ítem: la pantalla puede aplicar la misma a varios, pero el
/// contrato no tiene una categoría ni una marca "del lote".
/// </summary>
public record class IncorporarCatalogoArticulosDto
{
    public IReadOnlyList<ItemIncorporacionArticuloDto> Items { get; init; } = Array.Empty<ItemIncorporacionArticuloDto>();
}
