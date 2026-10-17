namespace Retail.Domain.Enums;

/// <summary>
/// Criterios de filtrado por nivel de inventario para la consulta del catálogo de artículos.
/// </summary>
public enum FiltroStockArticuloEnum
{
    Todos = 0,
    StockCritico = 1,
    SinStock = 2
}
