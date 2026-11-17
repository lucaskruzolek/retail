namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Criterios de la grilla de presupuestos con paginación en servidor (Ley 8).
/// </summary>
public record class ConsultaPresupuestosDto
{
    /// <summary>Busca por número de presupuesto, título o nombre del cliente.</summary>
    public string? TerminoBusqueda { get; init; }

    public FiltroEstadoPresupuestoEnum Filtro { get; init; } = FiltroEstadoPresupuestoEnum.Todos;

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}
