namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta recuperar para venta un presupuesto que ya fue convertido previamente o cancelado.
/// </summary>
public class PresupuestoYaConvertidoException : DomainException
{
    public int PresupuestoId { get; }

    public PresupuestoYaConvertidoException(int presupuestoId)
        : base($"El presupuesto ID {presupuestoId} ya fue convertido previamente a venta y no puede reutilizarse.")
    {
        PresupuestoId = presupuestoId;
    }

    public PresupuestoYaConvertidoException(string message)
        : base(message)
    {
    }
}
