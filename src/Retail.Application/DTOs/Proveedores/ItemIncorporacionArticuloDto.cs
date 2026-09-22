namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Parámetro individual para la incorporación de un ítem de catálogo con su porcentaje de ganancia específico.
/// </summary>
public record class ItemIncorporacionArticuloDto
{
    public required int IdCatalogo { get; init; }

    public required decimal PorcentajeGanancia { get; init; }
}
