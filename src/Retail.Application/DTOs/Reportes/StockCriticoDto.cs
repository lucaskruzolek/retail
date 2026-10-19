namespace Retail.Application.DTOs.Reportes;

/// <summary>Reporte de stock crítico y valorización del inventario (RF-25).</summary>
public record class StockCriticoDto : ResultadoReporteDto
{
    public override string Titulo => "Stock crítico y valorización";

    public required FiltroInventarioDto Filtro { get; init; }

    /// <summary>Filas ordenadas por faltante descendente.</summary>
    public IReadOnlyList<StockCriticoFilaDto> Filas { get; init; } = [];

    public int ArticulosConsiderados { get; init; }

    public decimal ValorizacionCosto { get; init; }

    public decimal ValorizacionVenta { get; init; }
}
