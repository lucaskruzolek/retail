using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Criterios de filtrado de pedidos con paginación en servidor (RF-28, Ley 8). Cada filtro es una lista de valores
/// admitidos (selección múltiple de los filtros de columna): una lista vacía no filtra, y dentro de una lista los
/// valores se combinan con O (<c>IN</c> en SQL). Filtros distintos se combinan con Y.
/// </summary>
public record class ConsultaPedidosDto
{
    /// <summary>Busca por razón social del proveedor o por número de pedido.</summary>
    public string? TerminoBusqueda { get; init; }

    public IReadOnlyList<EstadoPedidoEnum> Estados { get; init; } = [];

    public IReadOnlyList<int> IdsProveedor { get; init; } = [];

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}
