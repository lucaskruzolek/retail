using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Criterios de búsqueda y filtrado para el catálogo de artículos con paginación en servidor (Ley 8).
/// Cada filtro es una lista de valores admitidos (selección múltiple de los filtros de columna): una lista vacía no
/// filtra, y dentro de una lista los valores se combinan con O (<c>IN</c> en SQL). Filtros distintos se combinan con Y.
/// </summary>
public record class ConsultaArticulosDto
{
    /// <summary>Valor de <see cref="IdsProveedor"/> que representa "Ninguno (o manual)": artículos sin fuentes.</summary>
    public const int IdProveedorManual = -1;

    public string? TerminoBusqueda { get; init; }

    /// <summary>
    /// Categorías admitidas. Filtrar por rubro (RF-04, Rubro → Categoría) es elegir todas sus categorías: el filtro de
    /// la columna Categoría las agrupa por rubro.
    /// </summary>
    public IReadOnlyList<int> IdsCategoria { get; init; } = [];

    public IReadOnlyList<int> IdsMarca { get; init; } = [];

    /// <summary>
    /// Distribuidores admitidos; puede incluir <see cref="IdProveedorManual"/>. Cuenta cualquier fuente del artículo (no
    /// solo la principal) y, para una presentación derivada, las fuentes de su artículo origen.
    /// </summary>
    public IReadOnlyList<int> IdsProveedor { get; init; } = [];

    /// <summary>Estados de inventario admitidos. Con al menos uno, los servicios (que no llevan stock) quedan afuera.</summary>
    public IReadOnlyList<FiltroStockArticuloEnum> EstadosStock { get; init; } = [];

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 10;
}
