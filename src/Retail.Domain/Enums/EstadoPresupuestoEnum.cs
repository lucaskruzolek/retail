namespace Retail.Domain.Enums;

/// <summary>
/// Estados persistidos del ciclo de vida de un presupuesto comercial. "Vigente" y "vencido" no son estados:
/// la vigencia es derivada de la fecha de vencimiento (<c>Presupuesto.EstaVigente</c>, PS-08), porque un estado
/// que depende del reloj se desactualizaría solo.
/// </summary>
public enum EstadoPresupuestoEnum
{
    Pendiente = 1,
    Convertido = 2,
    Anulado = 4
}
