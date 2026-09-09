namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de una categoría para clasificación de artículos en el catálogo.
/// </summary>
public record class CategoriaDto
{
    public required int IdCategoria { get; init; }
    public required string NombreCategoria { get; init; }
}
