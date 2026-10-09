using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Imputación de pago realizada para cancelar el importe de una venta. Entidad interna del agregado
/// <see cref="Venta"/>: solo se crea a través de <see cref="Venta.ImputarPago"/>.
/// </summary>
public class PagoVenta : BaseEntity
{
    public int IdVenta { get; private set; }

    public Venta? Venta { get; private set; }

    public MedioPagoEnum MedioPago { get; private set; } = MedioPagoEnum.Efectivo;

    public decimal Monto { get; private set; }

    public string? ReferenciaPago { get; private set; }

    protected PagoVenta() { }

    internal PagoVenta(MedioPagoEnum medioPago, decimal monto, string? referenciaPago)
    {
        if (!Enum.IsDefined(medioPago))
        {
            throw new ArgumentOutOfRangeException(nameof(medioPago), "El medio de pago no es válido.");
        }

        if (monto <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(monto), "El monto del pago debe ser mayor a cero.");
        }

        MedioPago = medioPago;
        Monto = monto;
        ReferenciaPago = string.IsNullOrWhiteSpace(referenciaPago) ? null : referenciaPago.Trim();
    }
}
