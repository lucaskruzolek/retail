namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Alerta sobre variación entre el precio unitario pactado en el presupuesto y el precio actual de catálogo.
/// </summary>
public record class DiscrepanciaPrecioPresupuestoDto
{
    public required int IdArticulo { get; init; }
    public required string Descripcion { get; init; }
    public required decimal PrecioPactado { get; init; }
    public required decimal PrecioActualCatalogo { get; init; }
    public decimal DiferenciaUnitario => PrecioActualCatalogo - PrecioPactado;
    public bool HuboAumento => PrecioActualCatalogo > PrecioPactado;
}
