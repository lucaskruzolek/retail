namespace Retail.Domain.Enums;

/// <summary>
/// Estados de inventario por los que se filtra el catálogo de artículos. Son disjuntos (cada producto está en uno solo),
/// así que se pueden combinar en una selección múltiple: el "stock crítico" es <see cref="SinStock"/> +
/// <see cref="StockBajo"/>. Los servicios no llevan stock y no pertenecen a ninguno.
/// </summary>
public enum FiltroStockArticuloEnum
{
    /// <summary>Stock actual menor o igual a cero.</summary>
    SinStock = 1,

    /// <summary>Stock positivo pero que no supera el mínimo: hay que reponer.</summary>
    StockBajo = 2,

    /// <summary>Stock por encima del mínimo.</summary>
    ConStock = 3
}
