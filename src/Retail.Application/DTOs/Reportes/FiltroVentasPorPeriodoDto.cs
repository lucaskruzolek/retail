namespace Retail.Application.DTOs.Reportes;

/// <summary>Filtros del reporte de ventas por período (RF-24).</summary>
public record class FiltroVentasPorPeriodoDto : FiltroPeriodoDto
{
    public AgrupacionTemporalEnum Agrupacion { get; init; } = AgrupacionTemporalEnum.Dia;

    /// <summary>Cajero que registró las ventas; <c>null</c> para todos.</summary>
    public int? IdUsuario { get; init; }
}
