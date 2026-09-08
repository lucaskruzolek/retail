namespace Retail.Domain.Enums;

/// <summary>
/// Medios de pago aceptados en el punto de venta y cobranzas de cuentas corrientes.
/// </summary>
public enum MedioPagoEnum
{
    Efectivo = 1,
    TarjetaDebito = 2,
    TarjetaCredito = 3,
    TransferenciaQr = 4,
    CuentaCorriente = 5
}
