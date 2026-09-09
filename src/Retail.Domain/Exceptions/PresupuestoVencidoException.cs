namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta convertir a venta un presupuesto comercial cuya vigencia ha expirado sin conciliar precios actualizados.
/// </summary>
public class PresupuestoVencidoException : DomainException
{
    public int PresupuestoId { get; }
    public DateTime FechaVencimiento { get; }

    public PresupuestoVencidoException(int presupuestoId, DateTime fechaVencimiento)
        : base($"El presupuesto ID {presupuestoId} se encuentra vencido desde el {fechaVencimiento:dd/MM/yyyy HH:mm}. No se puede convertir a venta.")
    {
        PresupuestoId = presupuestoId;
        FechaVencimiento = fechaVencimiento;
    }

    public PresupuestoVencidoException(string message)
        : base(message)
    {
    }
}
