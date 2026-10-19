namespace Retail.Application.DTOs.Reportes.Consultas;

/// <summary>Un artículo con stock por debajo del mínimo.</summary>
public record StockCriticoFila(
    int IdArticulo,
    string Descripcion,
    string? CodigoBarras,
    string? Categoria,
    string? Marca,
    int StockActual,
    int StockMinimo,
    decimal CostoReposicion,
    decimal PrecioVenta);
