namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para fraccionar unidades de un artículo de origen en una de sus presentaciones (RF-21).
/// Se identifica la presentación derivada y no el origen, porque un origen puede tener varias presentaciones.
/// </summary>
public record class FraccionarDto
{
    public required int IdArticuloDerivado { get; init; }
    public required int CantidadOrigen { get; init; }
}
