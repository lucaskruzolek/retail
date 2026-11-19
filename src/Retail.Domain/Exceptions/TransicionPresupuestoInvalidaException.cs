using Retail.Domain.Enums;

namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta llevar un presupuesto a un estado que su ciclo de vida no permite
/// (por ejemplo, convertir uno ya convertido o anular uno ya anulado).
/// </summary>
public class TransicionPresupuestoInvalidaException : DomainException
{
    public EstadoPresupuestoEnum EstadoActual { get; }

    public TransicionPresupuestoInvalidaException(EstadoPresupuestoEnum estadoActual, string operacion)
        : base($"No se puede {operacion} un presupuesto en estado {estadoActual}.")
    {
        EstadoActual = estadoActual;
    }
}
