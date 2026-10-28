namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Cancelación de un pedido en borrador o emitido, con el motivo obligatorio (RF-28).
/// </summary>
public record class CancelarPedidoDto
{
    public int IdPedido { get; init; }

    public string Motivo { get; init; } = string.Empty;
}
