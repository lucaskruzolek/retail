using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Fiscal;

/// <summary>
/// Solicitud de autorización electrónica enviada al microservicio fiscal arcasdk / Web Service ARCA.
/// </summary>
public record class SolicitudCaeDto
{
    public required int IdVenta { get; init; }
    public required TipoComprobanteFiscalEnum TipoComprobante { get; init; }
    public required int PuntoVenta { get; init; }
    public string? CuitCliente { get; init; }
    public required decimal SubtotalNeto { get; init; }
    public required decimal IvaTotal { get; init; }
    public required decimal Total { get; init; }
    public required DateTime FechaComprobante { get; init; }
}
