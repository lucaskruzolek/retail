namespace Retail.Application.DTOs.Reportes;

/// <summary>Reporte de artículos sin movimiento (RF-25).</summary>
public record class SinMovimientoDto : ResultadoReporteDto
{
    public override string Titulo => "Artículos sin movimiento";

    public required FiltroSinMovimientoDto Filtro { get; init; }

    /// <summary>Primer día local a partir del cual se consideran las ventas; antes de esa fecha no hubo ninguna.</summary>
    public DateOnly SinVentasDesde { get; init; }

    /// <summary>Filas ordenadas por capital inmovilizado descendente.</summary>
    public IReadOnlyList<ArticuloSinMovimientoDto> Filas { get; init; } = [];

    public decimal CapitalInmovilizadoTotal { get; init; }
}
