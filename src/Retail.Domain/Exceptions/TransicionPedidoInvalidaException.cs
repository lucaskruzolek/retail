using Retail.Domain.Enums;

namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta llevar un pedido a proveedor a un estado que su ciclo de vida no permite
/// (por ejemplo, agregar renglones a un pedido ya emitido o recibir uno cancelado).
/// </summary>
public class TransicionPedidoInvalidaException : DomainException
{
    public EstadoPedidoEnum EstadoActual { get; }

    public TransicionPedidoInvalidaException(EstadoPedidoEnum estadoActual, string operacion)
        : base($"No se puede {operacion} un pedido en estado {estadoActual}.")
    {
        EstadoActual = estadoActual;
    }
}
