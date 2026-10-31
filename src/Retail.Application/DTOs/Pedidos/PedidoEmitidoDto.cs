namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Pedido emitido pendiente de recepción. Contrato de frontera con Compras (§5.2, RF-29).
/// </summary>
public record class PedidoEmitidoDto
{
    public required int IdPedido { get; init; }

    public required DateTime FechaEmision { get; init; }

    public required int CantidadRenglones { get; init; }

    public required decimal TotalEstimado { get; init; }
}
