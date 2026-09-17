namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de una marca o editorial para artículos en el catálogo.
/// </summary>
public record class MarcaDto
{
    public required int IdMarca { get; init; }
    public required string NombreMarca { get; init; }
}

