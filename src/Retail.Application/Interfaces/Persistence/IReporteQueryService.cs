using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Consultas;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Puerto de lectura de los reportes gerenciales (RF-24, RF-25). La implementación compone las consultas sobre
/// <c>IQueryable</c> con push-down a SQL y <c>AsNoTracking</c> (Ley 8). Incluye las ventas de artículos dados de baja
/// (D-8) y excluye las ventas e ítems con baja lógica. Devuelve importes sin redondear: eso lo hace el servicio.
/// </summary>
/// <remarks>
/// Las fechas de los filtros se ignoran: el rango viaja en <see cref="RangoUtcDto"/>.
/// </remarks>
public interface IReporteQueryService
{
    /// <summary>
    /// Fecha y hora UTC de la venta más antigua (sin contar las dadas de baja), o <c>null</c> si no hay ventas. Fija el
    /// límite inferior de los períodos (D-32).
    /// </summary>
    Task<DateTime?> ObtenerFechaPrimeraVentaAsync(CancellationToken cancellationToken = default);

    /// <summary>Totales de ventas y costo del rango.</summary>
    Task<ResumenVentasFila> ObtenerResumenVentasAsync(RangoUtcDto rango, CancellationToken cancellationToken = default);

    /// <summary>Ventas agrupadas por día, semana (lunes) o mes locales. Solo devuelve los períodos con ventas.</summary>
    /// <param name="incluirMediosDePago">
    /// <c>true</c> agrega el desglose del total por medio de pago (D-34), con una segunda consulta agregada sobre los
    /// pagos. Con <c>false</c> esa consulta no se ejecuta y los cinco importes por medio vienen en 0. Cada llamador
    /// decide: el tablero solo grafica el total y no los necesita.
    /// </param>
    Task<IReadOnlyList<VentasPeriodoFila>> ObtenerVentasPorPeriodoAsync(
        RangoUtcDto rango,
        AgrupacionTemporalEnum agrupacion,
        int? idUsuario,
        bool incluirMediosDePago,
        CancellationToken cancellationToken = default);

    /// <summary>Los artículos más vendidos según el criterio y la cantidad del filtro.</summary>
    Task<IReadOnlyList<RankingFila>> ObtenerMasVendidosAsync(
        RangoUtcDto rango,
        FiltroRankingDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Los artículos menos vendidos; incluye los artículos activos con cero ventas.</summary>
    Task<IReadOnlyList<RankingFila>> ObtenerMenosVendidosAsync(
        RangoUtcDto rango,
        FiltroRankingDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Ingreso, costo y unidades agrupados por categoría, marca o artículo.</summary>
    Task<IReadOnlyList<RentabilidadFila>> ObtenerRentabilidadAsync(
        RangoUtcDto rango,
        FiltroRentabilidadDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Cantidad de artículos, los que están bajo el mínimo y la valorización del stock.</summary>
    Task<ResumenInventarioFila> ObtenerResumenInventarioAsync(
        FiltroInventarioDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Artículos con stock por debajo del mínimo.</summary>
    Task<IReadOnlyList<StockCriticoFila>> ObtenerStockCriticoAsync(
        FiltroInventarioDto filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Artículos con stock y sin ventas desde <paramref name="sinVentasDesdeUtc"/>.</summary>
    Task<IReadOnlyList<SinMovimientoFila>> ObtenerSinMovimientoAsync(
        FiltroInventarioDto filtro,
        DateTime sinVentasDesdeUtc,
        CancellationToken cancellationToken = default);
}
