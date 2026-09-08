namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Parámetros para registrar y cobrar una venta atómica en mostrador (POS).
/// </summary>
public record class CrearVentaDto
{
    public required int IdTurno { get; init; }
    public required int IdUsuario { get; init; }
    public int? IdCliente { get; init; }
    public int? IdPresupuestoOrigen { get; init; }
    public decimal Descuento { get; init; } = 0;
    public required IReadOnlyList<DetalleVentaDto> Items { get; init; }
    public required IReadOnlyList<PagoVentaDto> Pagos { get; init; }
}
