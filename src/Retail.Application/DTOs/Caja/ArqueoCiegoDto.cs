namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Parámetros enviados por el cajero para el arqueo ciego al cerrar el turno.
/// El cajero únicamente declara el dinero físico contado.
/// </summary>
public record class ArqueoCiegoDto
{
    public required int IdTurno { get; init; }
    public required decimal SaldoDeclaradoEfectivo { get; init; }
    public decimal MontoRetenidoEnCaja { get; init; }
}
