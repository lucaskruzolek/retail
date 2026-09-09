using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Comprobante fiscal emitido o pendiente de autorización ante ARCA / AFIP.
/// </summary>
public class ComprobanteFiscal : BaseEntity
{
    public int IdVenta { get; set; }

    public Venta? Venta { get; set; }

    public TipoComprobanteFiscalEnum TipoComprobante { get; set; } = TipoComprobanteFiscalEnum.NoAplica;

    public int PuntoVenta { get; set; }

    public int NumeroComprobante { get; set; }

    public string? Cae { get; set; }

    public DateTime? FechaVtoCae { get; set; }

    public string? ResultadoArca { get; set; }

    public string? MotivoError { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
}
