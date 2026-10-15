namespace Retail.Application.DTOs.Reportes;

/// <summary>
/// Rango de consulta ya convertido a UTC. Lo arma <c>ReporteService</c> a partir de los días locales del filtro (D-9).
/// El puerto filtra <c>fecha_hora &gt;= DesdeUtc AND fecha_hora &lt; HastaExclusivoUtc</c> y agrupa sobre
/// <c>fecha_hora + DesfaseMinutos</c> (la hora local). Las fechas de los filtros se ignoran: el rango viaja aquí.
/// </summary>
/// <param name="DesdeUtc">Inicio del primer día local, en UTC (inclusive).</param>
/// <param name="HastaExclusivoUtc">Inicio del día local siguiente al último, en UTC (exclusive).</param>
/// <param name="DesfaseMinutos">Minutos a sumar a una fecha UTC para obtener la hora local (Argentina = -180).</param>
public record class RangoUtcDto(DateTime DesdeUtc, DateTime HastaExclusivoUtc, int DesfaseMinutos);
