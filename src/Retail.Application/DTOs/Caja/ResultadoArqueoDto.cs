namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Acta de cierre resultante tras el cálculo del arqueo ciego.
/// </summary>
public record class ResultadoArqueoDto
{
    public required int IdTurno { get; init; }
    public required decimal SaldoInicial { get; init; }
    public required decimal TotalVentasEfectivo { get; init; }
    public required decimal TotalIngresosEfectivo { get; init; }
    public required decimal TotalEgresosEfectivo { get; init; }
    public required decimal SaldoTeoricoEfectivo { get; init; }
    public required decimal SaldoDeclaradoEfectivo { get; init; }
    public required decimal DiferenciaEfectivo { get; init; }
    public required decimal TotalVentasElectronicas { get; init; }
    public required decimal MontoRetenidoEnCaja { get; init; }
    public required DateTime FechaCierre { get; init; }
    public bool HaySobrante => DiferenciaEfectivo > 0;
    public bool HayFaltante => DiferenciaEfectivo < 0;
}
