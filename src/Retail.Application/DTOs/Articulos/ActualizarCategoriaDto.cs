namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para actualizar una categoría (nombre y rubro).
/// </summary>
public record class ActualizarCategoriaDto
{
    public required int IdCategoria { get; init; }
    public required string Nombre { get; init; }
    public required int IdRubro { get; init; }
}
