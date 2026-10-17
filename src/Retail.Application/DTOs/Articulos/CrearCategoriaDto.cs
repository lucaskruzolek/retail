namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para dar de alta una categoría.
/// </summary>
public record class CrearCategoriaDto
{
    public required string Nombre { get; init; }
}
