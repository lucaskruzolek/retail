namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de una categoría para clasificación de artículos en el catálogo.
/// </summary>
public record class CategoriaDto
{
    public required int IdCategoria { get; init; }
    public required string NombreCategoria { get; init; }

    /// <summary>
    /// Rubro al que pertenece. En los combos que no lo necesitan queda en 0 y <see cref="NombreRubro"/> en nulo.
    /// </summary>
    public int IdRubro { get; init; }

    public string? NombreRubro { get; init; }

    /// <summary>
    /// Artículos asignados, incluidos los dados de baja (D-1, R-1). Solo se completa en el listado de
    /// administración; en los combos queda en 0.
    /// </summary>
    public int CantidadEnUso { get; init; }
}

