namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Criterios de búsqueda y filtrado para el catálogo de artículos con paginación en servidor (Ley 8).
/// </summary>
public record class ConsultaArticulosDto
{
    public string? TerminoBusqueda { get; init; }

    public int? IdCategoria { get; init; }

    public bool SoloStockCritico { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 10;
}
