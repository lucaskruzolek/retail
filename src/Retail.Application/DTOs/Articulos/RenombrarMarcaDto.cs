namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para cambiar el nombre de una marca.
/// </summary>
public record class RenombrarMarcaDto
{
    public required int IdMarca { get; init; }
    public required string Nombre { get; init; }
}
