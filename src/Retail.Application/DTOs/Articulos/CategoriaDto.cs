namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de una categoría para clasificación de artículos en el catálogo.
/// </summary>
public record class CategoriaDto
{
    public required int IdCategoria { get; init; }
    public required string NombreCategoria { get; init; }

    /// <summary>
    /// Artículos asignados, incluidos los dados de baja (D-1, R-1). Solo se completa en el listado de
    /// administración; en los combos queda en 0.
    /// </summary>
    public int CantidadEnUso { get; init; }
}

