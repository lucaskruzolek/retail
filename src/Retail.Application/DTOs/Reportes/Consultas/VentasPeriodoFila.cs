namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Ventas de un período local (día, semana o mes), sin redondear.</summary>
/// <param name="InicioPeriodo">El día local, el lunes de la semana local o el día 1 del mes local.</param>
/// <param name="CantidadVentas">Ventas del período.</param>
/// <param name="TotalVendido">Ingreso neto del período.</param>
/// <param name="Descuentos">Descuentos globales del período.</param>
/// <param name="Efectivo">Importe cobrado en efectivo en el período (D-34).</param>
/// <param name="TarjetaDebito">Importe cobrado con tarjeta de débito.</param>
/// <param name="TarjetaCredito">Importe cobrado con tarjeta de crédito.</param>
/// <param name="TransferenciaQr">Importe cobrado por transferencia o QR.</param>
/// <param name="CuentaCorriente">Importe imputado a la cuenta corriente del cliente.</param>
/// <remarks>
/// Los cinco medios son propiedades explícitas y no un diccionario: <c>MedioPagoEnum</c> es un enum estable del Dominio,
/// y así cada columna del reporte es un campo tipado. Su suma coincide con <see cref="TotalVendido"/> (Venta.ValidarCierre).
/// </remarks>
public record VentasPeriodoFila(
    DateOnly InicioPeriodo,
    int CantidadVentas,
    decimal TotalVendido,
    decimal Descuentos,
    decimal Efectivo = 0m,
    decimal TarjetaDebito = 0m,
    decimal TarjetaCredito = 0m,
    decimal TransferenciaQr = 0m,
    decimal CuentaCorriente = 0m);
