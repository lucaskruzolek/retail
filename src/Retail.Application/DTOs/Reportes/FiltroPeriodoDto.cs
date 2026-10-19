namespace Retail.Application.DTOs.Reportes;

/// <summary>
/// Período de un reporte. Las fechas son días calendario LOCALES del comercio, ambos extremos incluidos (D-9).
/// </summary>
public record class FiltroPeriodoDto
{
    public DateOnly Desde { get; init; }

    public DateOnly Hasta { get; init; }
}
