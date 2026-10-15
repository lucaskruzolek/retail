using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Reportes gerenciales (RF-24, RF-25). Todas las operaciones son exclusivas del Gerente (D-11) y reciben días
/// calendario locales, que el servicio convierte a UTC (D-9).
/// </summary>
public interface IReporteService
{
    /// <summary>Tablero con los indicadores del período y su comparación con el período anterior.</summary>
    Task<TableroDto> ObtenerTableroAsync(int idActor, FiltroPeriodoDto filtro, CancellationToken cancellationToken = default);

    /// <summary>Ventas agrupadas por día, semana o mes.</summary>
    Task<VentasPorPeriodoDto> ObtenerVentasPorPeriodoAsync(
        int idActor,
        FiltroVentasPorPeriodoDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Artículos más y menos vendidos.</summary>
    Task<RankingArticulosDto> ObtenerRankingAsync(int idActor, FiltroRankingDto filtro, CancellationToken cancellationToken = default);

    /// <summary>Margen bruto por categoría, marca o artículo.</summary>
    Task<RentabilidadDto> ObtenerRentabilidadAsync(
        int idActor,
        FiltroRentabilidadDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Artículos bajo el stock mínimo y valorización del inventario.</summary>
    Task<StockCriticoDto> ObtenerStockCriticoAsync(int idActor, FiltroInventarioDto filtro, CancellationToken cancellationToken = default);

    /// <summary>Artículos con stock que no se venden hace tiempo.</summary>
    Task<SinMovimientoDto> ObtenerSinMovimientoAsync(
        int idActor,
        FiltroSinMovimientoDto filtro,
        CancellationToken cancellationToken = default);
}
