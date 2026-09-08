namespace Retail.Domain.Enums;

/// <summary>
/// Estados de ciclo de vida de un presupuesto comercial (validez 15 días).
/// </summary>
public enum EstadoPresupuestoEnum
{
    Pendiente = 1,
    Convertido = 2,
    Vencido = 3,
    Cancelado = 4
}
