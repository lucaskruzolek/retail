namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para cambiar el nombre de un rubro.
/// </summary>
public record class RenombrarRubroDto
{
    public required int IdRubro { get; init; }
    public required string Nombre { get; init; }
}
