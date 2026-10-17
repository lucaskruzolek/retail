namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para dar de alta una marca.
/// </summary>
public record class CrearMarcaDto
{
    public required string Nombre { get; init; }
}
