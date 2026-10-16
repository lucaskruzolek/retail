namespace Retail.Application.DTOs.Reportes;

/// <summary>Filtros del reporte de artículos sin movimiento (RF-25).</summary>
public record class FiltroSinMovimientoDto : FiltroInventarioDto
{
    /// <summary>Días sin ventas que se consideran inmovilizados (entre 1 y 3650).</summary>
    public int Dias { get; init; } = 90;
}
