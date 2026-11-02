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

    /// <summary>
    /// Costo de reposición vigente del artículo: permite anticipar si la compra cambiará el precio (RF-19).
    /// </summary>
    public required decimal CostoReposicionActual { get; init; }

    /// <summary>
    /// Markup vigente del artículo, el mismo que aplicará el Dominio al recalcular el precio de venta (RF-19).
    /// </summary>
    public required decimal PorcentajeGanancia { get; init; }

    public required decimal PrecioVentaActual { get; init; }
}
