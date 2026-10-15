namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Ventas de un período local (día, semana o mes), sin redondear.</summary>
/// <param name="InicioPeriodo">El día local, el lunes de la semana local o el día 1 del mes local.</param>
/// <param name="CantidadVentas">Ventas del período.</param>
/// <param name="TotalVendido">Ingreso neto del período.</param>
/// <param name="Descuentos">Descuentos globales del período.</param>
public record VentasPeriodoFila(DateOnly InicioPeriodo, int CantidadVentas, decimal TotalVendido, decimal Descuentos);
