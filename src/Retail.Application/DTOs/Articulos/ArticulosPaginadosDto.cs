using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Resultado paginado de la consulta de catálogo de artículos con métricas de cabecera y push-down a SQL Server (Ley 8).
/// </summary>
public record class ArticulosPaginadosDto : BaseDto
{
    public IReadOnlyList<ArticuloDto> Items { get; init; } = Array.Empty<ArticuloDto>();

    public int TotalRegistros { get; init; }

    public int TotalArticulos { get; init; }

    public int TotalAlertasStock { get; init; }

    public int PaginaActual { get; init; }

    public int TamanoPagina { get; init; }

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling((double)TotalRegistros / Math.Max(1, TamanoPagina)));
}
