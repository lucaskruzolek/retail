namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando una operación en cuenta corriente supera el límite de crédito comercial autorizado para el cliente.
/// </summary>
public class LimiteCreditoExcedidoException : DomainException
{
    public int ClienteId { get; }
    public decimal SaldoActual { get; }
    public decimal LimiteCredito { get; }
    public decimal MontoSolicitado { get; }

    public LimiteCreditoExcedidoException(int clienteId, decimal saldoActual, decimal limiteCredito, decimal montoSolicitado)
        : base($"Límite de crédito excedido para el cliente ID {clienteId}. Saldo actual: {saldoActual:C}, límite: {limiteCredito:C}, monto solicitado: {montoSolicitado:C}.")
    {
        ClienteId = clienteId;
        SaldoActual = saldoActual;
        LimiteCredito = limiteCredito;
        MontoSolicitado = montoSolicitado;
    }

    public LimiteCreditoExcedidoException(string message)
        : base(message)
    {
    }
}
