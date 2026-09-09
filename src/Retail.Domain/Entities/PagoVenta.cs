using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Imputación de pago realizada para cancelar el importe de una venta.
/// </summary>
public class PagoVenta : BaseEntity
{
    public int IdVenta { get; set; }

    public Venta? Venta { get; set; }

    public MedioPagoEnum MedioPago { get; set; } = MedioPagoEnum.Efectivo;

    public decimal Monto { get; set; }

    public string? ReferenciaPago { get; set; }
}
