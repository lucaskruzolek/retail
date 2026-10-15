using Retail.Domain.Enums;

namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta llevar un registro de backup a un estado que su ciclo de vida no permite
/// (por ejemplo, verificar una copia que falló o purgar una que todavía se está escribiendo).
/// </summary>
public class TransicionBackupInvalidaException : DomainException
{
    public EstadoBackupEnum EstadoActual { get; }

    public TransicionBackupInvalidaException(EstadoBackupEnum estadoActual, string operacion)
        : base($"No se puede {operacion} una copia de seguridad en estado {estadoActual}.")
    {
        EstadoActual = estadoActual;
    }
}
