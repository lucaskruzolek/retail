using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Criterios de filtrado de pedidos con paginación en servidor (RF-28, Ley 8).
/// </summary>
public record class ConsultaPedidosDto
{
    public EstadoPedidoEnum? Estado { get; init; }

    public int? IdProveedor { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}
