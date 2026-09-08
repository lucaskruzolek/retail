using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Fiscal;

/// <summary>
/// Representación de un comprobante fiscal asociado a una venta (autorizado o en contingencia).
/// </summary>
public record class ComprobanteFiscalDto
{
    public required int IdComprobante { get; init; }
    public required int IdVenta { get; init; }
    public required TipoComprobanteFiscalEnum TipoComprobante { get; init; }
    public required int PuntoVenta { get; init; }
    public int? NumeroComprobante { get; init; }
    public string? Cae { get; init; }
    public DateOnly? FechaVtoCae { get; init; }
    public string? ResultadoArca { get; init; }
    public string? MotivoError { get; init; }
    public required DateTime FechaEmision { get; init; }
    public required EstadoFiscalEnum EstadoFiscal { get; init; }
}
