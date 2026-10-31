namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Renglón de un pedido listo para precargar una compra. Contrato de frontera (§5.2, RF-29).
/// </summary>
public record class RenglonPedidoParaCompraDto
{
    public required int IdArticulo { get; init; }

    public string? CodigoBarras { get; init; }

    public required string DescripcionArticulo { get; init; }

    public required string CodigoProveedor { get; init; }

    public required int Cantidad { get; init; }

    public required decimal CostoPactado { get; init; }
}
