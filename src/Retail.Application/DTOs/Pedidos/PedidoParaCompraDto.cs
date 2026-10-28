namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Pedido emitido con los renglones que Compras precarga al registrar la recepción. Contrato de frontera (§5.2, RF-29).
/// </summary>
public record class PedidoParaCompraDto
{
    public required int IdPedido { get; init; }

    public required int IdProveedor { get; init; }

    public required IReadOnlyList<RenglonPedidoParaCompraDto> Renglones { get; init; }
}
