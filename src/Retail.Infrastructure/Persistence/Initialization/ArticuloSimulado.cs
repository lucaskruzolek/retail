namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Cómo se comporta un artículo durante la simulación. La fila real se recarga cada día; esto es lo que no cambia.
/// </summary>
/// <param name="Peso">Popularidad relativa: la probabilidad de aparecer en una venta.</param>
/// <param name="UnidadesPorSemana">Demanda semanal estimada, con la que se decide cuándo reponer.</param>
internal sealed record ArticuloSimulado(
    int Id,
    string Descripcion,
    PerfilDemandaDemo Perfil,
    double Peso,
    int UnidadesPorSemana,
    int StockMinimo,
    int StockObjetivo,
    int? IdProveedor,
    int? IdFuente,
    string? CodigoProveedor,
    int? IdOrigen,
    int? UnidadesPorOrigen);
