namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para dar de alta un rubro.
/// </summary>
public record class CrearRubroDto
{
    public required string Nombre { get; init; }
}
