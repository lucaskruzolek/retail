namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Criterios de la grilla de presupuestos con paginación en servidor (Ley 8). Los filtros de columna son de selección
/// múltiple: dentro de un filtro los valores elegidos se combinan con O, y filtros distintos se combinan con Y.
/// </summary>
public record class ConsultaPresupuestosDto
{
    /// <summary>Busca por número de presupuesto, título o nombre del cliente.</summary>
    public string? TerminoBusqueda { get; init; }

    /// <summary>Estados admitidos (selección múltiple); una lista vacía no filtra por estado.</summary>
    public IReadOnlyList<FiltroEstadoPresupuestoEnum> Estados { get; init; } = [];

    /// <summary>Clientes admitidos; vacía y sin <see cref="IncluirConsumidorFinal"/>, no filtra por cliente.</summary>
    public IReadOnlyList<int> IdsCliente { get; init; } = [];

    /// <summary>
    /// Incluye los presupuestos sin cliente (Consumidor Final). Con <see cref="IdsCliente"/> la condición es "el cliente
    /// está en la lista <b>o</b> no tiene cliente".
    /// </summary>
    public bool IncluirConsumidorFinal { get; init; }

    /// <summary>
    /// Rango de la fecha de emisión como intervalo semiabierto en UTC: <c>[EmisionDesdeUtc, EmisionHastaUtc)</c>. Un
    /// extremo <c>null</c> no pone tope.
    /// </summary>
    public DateTime? EmisionDesdeUtc { get; init; }

    /// <summary>Extremo superior <b>exclusivo</b> del rango de emisión (el inicio del día siguiente al último elegido).</summary>
    public DateTime? EmisionHastaUtc { get; init; }

    /// <summary>Primer día de vencimiento incluido (fecha calendario local); <c>null</c> no pone tope.</summary>
    public DateOnly? VenceDesde { get; init; }

    /// <summary>Último día de vencimiento incluido (fecha calendario local); <c>null</c> no pone tope.</summary>
    public DateOnly? VenceHasta { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}
