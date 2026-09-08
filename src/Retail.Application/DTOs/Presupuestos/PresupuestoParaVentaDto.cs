namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Proyección de presupuesto validado para recuperación inmediata en el POS.
/// Incluye auditoría de disponibilidad de stock y advertencias de discrepancia de precios.
/// </summary>
public record class PresupuestoParaVentaDto
{
    public required int IdPresupuesto { get; init; }
    public int? IdCliente { get; init; }
    public string? ClienteNombre { get; init; }
    public required decimal TotalPactado { get; init; }
    public required bool StockDisponible { get; init; }
    public required IReadOnlyList<DetallePresupuestoDto> Items { get; init; }
    public required IReadOnlyList<DiscrepanciaPrecioPresupuestoDto> DiscrepanciasPrecios { get; init; }
    public bool RequiereConfirmacionCajero => DiscrepanciasPrecios.Count > 0;
}
