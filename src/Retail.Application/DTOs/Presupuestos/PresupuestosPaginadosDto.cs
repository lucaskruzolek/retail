using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Resultado paginado de la consulta de presupuestos con push-down a SQL Server (Ley 8).
/// </summary>
public record class PresupuestosPaginadosDto : BaseDto
{
    public IReadOnlyList<PresupuestoResumenDto> Items { get; init; } = Array.Empty<PresupuestoResumenDto>();

    public int TotalRegistros { get; init; }

    public int PaginaActual { get; init; }

    public int TamanoPagina { get; init; }

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling((double)TotalRegistros / Math.Max(1, TamanoPagina)));
}
