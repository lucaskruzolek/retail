namespace Retail.Application.DTOs.Reportes;

/// <summary>Filtros del ranking de artículos más y menos vendidos (RF-24).</summary>
public record class FiltroRankingDto : FiltroPeriodoDto
{
    public int? IdCategoria { get; init; }

    public int? IdMarca { get; init; }

    public CriterioRankingEnum Criterio { get; init; } = CriterioRankingEnum.Unidades;

    /// <summary>Cantidad de artículos de cada ranking (entre 1 y 100).</summary>
    public int Cantidad { get; init; } = 10;
}
