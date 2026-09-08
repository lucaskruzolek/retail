namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando un retiro de mostrador supera el dinero físico disponible en gaveta.
/// </summary>
public class SaldoCajaInsuficienteException : DomainException
{
    public int TurnoId { get; }
    public decimal SaldoDisponible { get; }
    public decimal MontoRetiro { get; }

    public SaldoCajaInsuficienteException(int turnoId, decimal saldoDisponible, decimal montoRetiro)
        : base($"Saldo de efectivo insuficiente en el turno ID {turnoId}. Efectivo disponible: {saldoDisponible:C}, monto a retirar: {montoRetiro:C}.")
    {
        TurnoId = turnoId;
        SaldoDisponible = saldoDisponible;
        MontoRetiro = montoRetiro;
    }

    public SaldoCajaInsuficienteException(string message)
        : base(message)
    {
    }
}
