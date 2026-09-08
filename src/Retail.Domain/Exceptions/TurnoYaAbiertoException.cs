namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta abrir un turno de caja existiendo ya un turno activo en mostrador.
/// </summary>
public class TurnoYaAbiertoException : DomainException
{
    public int TurnoActivoId { get; }

    public TurnoYaAbiertoException(int turnoActivoId)
        : base($"Ya existe un turno de caja abierto (ID {turnoActivoId}). No se puede abrir un nuevo turno simultáneo.")
    {
        TurnoActivoId = turnoActivoId;
    }

    public TurnoYaAbiertoException(string message)
        : base(message)
    {
    }
}
