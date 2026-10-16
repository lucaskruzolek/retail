namespace Retail.Application.DTOs.Reportes;

/// <summary>Reporte de artículos más y menos vendidos (RF-24).</summary>
public record class RankingArticulosDto : ResultadoReporteDto
{
    public override string Titulo => "Ranking de artículos";

    public required FiltroRankingDto Filtro { get; init; }

    public IReadOnlyList<RankingArticuloDto> MasVendidos { get; init; } = [];

    public IReadOnlyList<RankingArticuloDto> MenosVendidos { get; init; } = [];
}
