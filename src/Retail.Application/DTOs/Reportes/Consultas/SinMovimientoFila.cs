namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Un artículo con stock que no se vende desde cierta fecha. <c>UltimaVentaUtc</c> es <c>null</c> si nunca se vendió.</summary>
public record SinMovimientoFila(
    int IdArticulo,
    string Descripcion,
    string? CodigoBarras,
    string? Categoria,
    string? Marca,
    int StockActual,
    decimal CostoReposicion,
    DateTime? UltimaVentaUtc);
