using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Resultado paginado de la consulta de listas con push-down a SQL Server (Ley 8).
/// </summary>
public record class ListasPaginadosDto : BaseDto
{
    public IReadOnlyList<ListaResumenDto> Items { get; init; } = Array.Empty<ListaResumenDto>();

    public int TotalRegistros { get; init; }

    public int PaginaActual { get; init; }

    public int TamanoPagina { get; init; }

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling((double)TotalRegistros / Math.Max(1, TamanoPagina)));
}
