namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta registrar una venta, cobro o movimiento sin un turno de caja abierto.
/// </summary>
public class CajaCerradaException : DomainException
{
    public CajaCerradaException()
        : base("No existe un turno de caja abierto para registrar operaciones en mostrador.")
    {
    }

    public CajaCerradaException(string message)
        : base(message)
    {
    }
}
