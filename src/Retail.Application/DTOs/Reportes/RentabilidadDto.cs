namespace Retail.Application.DTOs.Reportes;

/// <summary>Reporte de rentabilidad (RF-25).</summary>
public record class RentabilidadDto : ResultadoReporteDto
{
    public override string Titulo => "Rentabilidad";

    public required FiltroRentabilidadDto Filtro { get; init; }

    /// <summary>Filas ordenadas por margen bruto descendente.</summary>
    public IReadOnlyList<RentabilidadFilaDto> Filas { get; init; } = [];

    /// <summary>Fila de totales, calculada sumando las filas crudas antes de redondear.</summary>
    public required RentabilidadFilaDto Total { get; init; }
}
