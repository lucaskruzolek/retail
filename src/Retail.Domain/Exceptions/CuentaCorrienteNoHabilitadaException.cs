namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta imputar una venta o cobro a cuenta corriente para un cliente inhabilitado.
/// </summary>
public class CuentaCorrienteNoHabilitadaException : DomainException
{
    public int ClienteId { get; }

    public CuentaCorrienteNoHabilitadaException(int clienteId)
        : base($"El cliente ID {clienteId} no tiene habilitada una cuenta corriente comercial.")
    {
        ClienteId = clienteId;
    }

    public CuentaCorrienteNoHabilitadaException(string message)
        : base(message)
    {
    }
}
