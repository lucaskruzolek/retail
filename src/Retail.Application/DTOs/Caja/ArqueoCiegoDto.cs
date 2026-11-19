namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Parámetros enviados por quien cierra el turno para el arqueo ciego. Declara únicamente el dinero físico contado;
/// además trae la "versión" del turno que vio (tres valores), para que el cierre falle si el turno siguió operando.
/// </summary>
public record class ArqueoCiegoDto
{
    public required int IdTurno { get; init; }

    /// <summary>
    /// Quien ejecuta el cierre: el dueño del turno o un Gerente (MT-14).
    /// </summary>
    public required int IdUsuarioActor { get; init; }

    public required decimal SaldoDeclaradoEfectivo { get; init; }
    public decimal MontoRetenidoEnCaja { get; init; }

    /// <summary>Saldo teórico de efectivo que vio quien cierra (control optimista).</summary>
    public required decimal SaldoTeoricoLeido { get; init; }

    /// <summary>Total de ventas electrónicas que vio quien cierra (control optimista).</summary>
    public required decimal TotalElectronicoLeido { get; init; }

    /// <summary>Cantidad de ventas que vio quien cierra (control optimista).</summary>
    public required int CantidadVentasLeida { get; init; }
}
