using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Criterios de búsqueda y filtrado para el catálogo de artículos con paginación en servidor (Ley 8).
/// </summary>
public record class ConsultaArticulosDto
{
    public string? TerminoBusqueda { get; init; }

    public int? IdCategoria { get; init; }

    public int? IdMarca { get; init; }

    /// <summary>
    /// Identificador del distribuidor para filtrar.
    /// null = Todos, -1 = Ninguno (Manual), > 0 = Id del Proveedor específico.
    /// Para presentaciones derivadas, se evalúa contra el distribuidor del artículo origen.
    /// </summary>
    public int? IdProveedor { get; init; }

    public FiltroStockArticuloEnum FiltroStock { get; init; } = FiltroStockArticuloEnum.Todos;

    /// <summary>
    /// Compatibilidad hacia atrás: si se establece true, equivale a FiltroStockArticuloEnum.StockCritico.
    /// </summary>
    public bool SoloStockCritico
    {
        get => FiltroStock == FiltroStockArticuloEnum.StockCritico;
        init => FiltroStock = value ? FiltroStockArticuloEnum.StockCritico : FiltroStockArticuloEnum.Todos;
    }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 10;
}
