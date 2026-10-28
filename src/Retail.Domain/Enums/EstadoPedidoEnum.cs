namespace Retail.Domain.Enums;

/// <summary>
/// Ciclo de vida de un pedido a proveedor (RF-28): nace en <c>Borrador</c>, se emite al proveedor y termina
/// <c>Recibido</c> (cuando Compras registra la mercadería) o <c>Cancelado</c>.
/// </summary>
public enum EstadoPedidoEnum
{
    Borrador = 1,
    Emitido = 2,
    Recibido = 3,
    Cancelado = 4
}
