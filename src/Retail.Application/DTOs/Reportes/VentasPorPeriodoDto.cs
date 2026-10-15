namespace Retail.Application.DTOs.Reportes;

/// <summary>Reporte de ventas por período (RF-24).</summary>
public record class VentasPorPeriodoDto : ResultadoReporteDto
{
    public override string Titulo => "Ventas por período";

    public required FiltroVentasPorPeriodoDto Filtro { get; init; }

    public IReadOnlyList<PuntoVentasDto> Puntos { get; init; } = [];

    public decimal TotalVendido { get; init; }

    public int CantidadVentas { get; init; }

    public decimal Descuentos { get; init; }
}
