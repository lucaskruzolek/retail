namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando una cobranza supera el saldo deudor actual de la cuenta corriente del cliente.
/// </summary>
public class CobranzaExcedeDeudaException : DomainException
{
    public int ClienteId { get; }
    public decimal SaldoDeudor { get; }
    public decimal MontoCobranza { get; }

    public CobranzaExcedeDeudaException(int clienteId, decimal saldoDeudor, decimal montoCobranza)
        : base($"El monto a cobrar ({montoCobranza:C}) excede la deuda actual ({saldoDeudor:C}) del cliente ID {clienteId}.")
    {
        ClienteId = clienteId;
        SaldoDeudor = saldoDeudor;
        MontoCobranza = montoCobranza;
    }

    public CobranzaExcedeDeudaException(string message)
        : base(message)
    {
    }
}
