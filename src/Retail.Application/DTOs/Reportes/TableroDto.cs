namespace Retail.Application.DTOs.Reportes;

/// <summary>Tablero gerencial (RF-24, RF-25): indicadores del período comparados con el período anterior.</summary>
public record class TableroDto : ResultadoReporteDto
{
    public override string Titulo => "Tablero gerencial";

    public required FiltroPeriodoDto Filtro { get; init; }

    public DateOnly DesdeAnterior { get; init; }

    public DateOnly HastaAnterior { get; init; }

    public required IndicadorDto TotalVendido { get; init; }

    public required IndicadorDto CantidadVentas { get; init; }

    public required IndicadorDto MargenBruto { get; init; }

    /// <summary>Margen sobre lo vendido en el período; <c>null</c> si no hubo ventas.</summary>
    public decimal? MargenPorcentual { get; init; }

    public decimal? MargenPorcentualAnterior { get; init; }

    /// <summary>Diferencia en puntos porcentuales; <c>null</c> si alguno de los dos márgenes es <c>null</c>.</summary>
    public decimal? VariacionMargenPuntos { get; init; }

    public int ArticulosBajoStockMinimo { get; init; }

    public decimal ValorizacionInventarioCosto { get; init; }

    public IReadOnlyList<PuntoVentasDto> EvolucionVentas { get; init; } = [];

    public AgrupacionTemporalEnum AgrupacionEvolucion { get; init; }

    public IReadOnlyList<RentabilidadFilaDto> MargenPorCategoria { get; init; } = [];
}
