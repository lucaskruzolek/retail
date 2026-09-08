namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta cerrar o registrar operaciones en un turno de caja que ya fue cerrado.
/// </summary>
public class TurnoYaCerradoException : DomainException
{
    public int TurnoId { get; }

    public TurnoYaCerradoException(int turnoId)
        : base($"El turno de caja ID {turnoId} ya se encuentra cerrado.")
    {
        TurnoId = turnoId;
    }

    public TurnoYaCerradoException(string message)
        : base(message)
    {
    }
}
