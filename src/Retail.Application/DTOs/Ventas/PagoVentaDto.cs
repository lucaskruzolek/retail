using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Imputación de medio de pago para cancelar el importe de una venta.
/// </summary>
public record class PagoVentaDto
{
    public required MedioPagoEnum MedioPago { get; init; }
    public required decimal Monto { get; init; }
    public string? ReferenciaPago { get; init; }
    public decimal? MontoRecibido { get; init; }
    public decimal? Vuelto { get; init; }
}
