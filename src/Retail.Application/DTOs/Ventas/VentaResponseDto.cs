using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Comprobante de resultado tras registrar y persistir una venta.
/// </summary>
public record class VentaResponseDto
{
    public required int IdVenta { get; init; }
    public required int IdTurno { get; init; }
    public required int IdUsuario { get; init; }
    public int? IdCliente { get; init; }
    public string? ClienteNombre { get; init; }
    public int? IdPresupuestoOrigen { get; init; }
    public required DateTime FechaHora { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Descuento { get; init; }
    public required decimal Total { get; init; }
    public required EstadoFiscalEnum EstadoFiscal { get; init; }
    public required IReadOnlyList<DetalleVentaDto> Items { get; init; }
    public required IReadOnlyList<PagoVentaDto> Pagos { get; init; }
}
