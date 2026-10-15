namespace Retail.Application.DTOs.Reportes;

/// <summary>Filtros del reporte de rentabilidad (RF-25).</summary>
public record class FiltroRentabilidadDto : FiltroPeriodoDto
{
    public int? IdCategoria { get; init; }

    public int? IdMarca { get; init; }

    public AgrupacionRentabilidadEnum AgruparPor { get; init; } = AgrupacionRentabilidadEnum.Categoria;
}
