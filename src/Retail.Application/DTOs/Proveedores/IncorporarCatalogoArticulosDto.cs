namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Parámetros para incorporar uno o varios ítems del catálogo de un proveedor a la tabla propia de Artículos.
/// </summary>
public record class IncorporarCatalogoArticulosDto
{
    public required IReadOnlyList<int> IdsCatalogo { get; init; }
    
    public required int IdCategoria { get; init; }
    
    public required int IdMarca { get; init; }
    
    public required decimal PorcentajeGananciaSugerido { get; init; }
}
