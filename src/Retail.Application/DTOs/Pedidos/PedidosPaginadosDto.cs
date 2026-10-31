using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Resultado paginado de la consulta de pedidos con push-down a SQL Server (RF-28, Ley 8).
/// </summary>
public record class PedidosPaginadosDto : BaseDto
{
    public IReadOnlyList<PedidoResumenDto> Items { get; init; } = Array.Empty<PedidoResumenDto>();

    public int TotalRegistros { get; init; }

    public int PaginaActual { get; init; }

    public int TamanoPagina { get; init; }

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling((double)TotalRegistros / Math.Max(1, TamanoPagina)));
}
