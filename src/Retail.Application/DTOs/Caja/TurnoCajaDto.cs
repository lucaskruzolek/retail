using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Información resumida del turno de caja activo o histórico.
/// </summary>
public record class TurnoCajaDto
{
    public required int IdTurno { get; init; }
    public required int IdUsuario { get; init; }
    public string? NombreUsuario { get; init; }
    public required DateTime FechaApertura { get; init; }
    public required decimal SaldoInicial { get; init; }
    public DateTime? FechaCierre { get; init; }
    public decimal TotalVentasEfectivo { get; init; }
    public decimal TotalIngresosEfectivo { get; init; }
    public decimal TotalEgresosEfectivo { get; init; }
    public decimal SaldoTeoricoEfectivo { get; init; }
    public decimal? SaldoDeclaradoEfectivo { get; init; }
    public decimal? DiferenciaEfectivo { get; init; }
    public decimal TotalVentasElectronicas { get; init; }
    public decimal? MontoRetenidoEnCaja { get; init; }
    public required EstadoTurnoEnum Estado { get; init; }
}
