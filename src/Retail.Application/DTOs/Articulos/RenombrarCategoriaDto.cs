namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para cambiar el nombre de una categoría.
/// </summary>
public record class RenombrarCategoriaDto
{
    public required int IdCategoria { get; init; }
    public required string Nombre { get; init; }
}
