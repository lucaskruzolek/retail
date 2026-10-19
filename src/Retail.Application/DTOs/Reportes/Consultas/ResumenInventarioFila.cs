namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Totales del inventario vigente, sin redondear.</summary>
public record ResumenInventarioFila(
    int ArticulosConsiderados,
    int ArticulosBajoMinimo,
    decimal ValorizacionCosto,
    decimal ValorizacionVenta);
