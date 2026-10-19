namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Totales de ventas de un rango, sin redondear. <c>TotalVendido</c> ya es neto de descuento.</summary>
/// <param name="CantidadVentas">Ventas del rango.</param>
/// <param name="TotalVendido">Suma de <c>Venta.Total</c> (ingreso neto).</param>
/// <param name="Descuentos">Suma de los descuentos globales.</param>
/// <param name="CostoTotal">Suma de costo unitario histórico por cantidad.</param>
public record ResumenVentasFila(int CantidadVentas, decimal TotalVendido, decimal Descuentos, decimal CostoTotal);
