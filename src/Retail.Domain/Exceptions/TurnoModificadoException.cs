namespace Retail.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando el turno registró operaciones (ventas, cobranzas o movimientos de efectivo) después de que
/// quien lo cierra leyó sus importes. Evita un arqueo ciego contra un saldo teórico desactualizado.
/// </summary>
public class TurnoModificadoException : DomainException
{
    public TurnoModificadoException()
        : base("El turno registró operaciones después de que se cargó el cierre. Vuelva a abrir el cierre para ver los importes actualizados.")
    {
    }
}
