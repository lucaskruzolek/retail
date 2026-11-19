namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta operar (vender, mover efectivo, cobrar) sobre el turno de caja de otro cajero.
/// Solo el dueño de un turno abierto lo opera; un Gerente únicamente puede cerrarlo (MT-14).
/// </summary>
public class TurnoAjenoException : DomainException
{
    public TurnoAjenoException(string message)
        : base(message)
    {
    }
}
