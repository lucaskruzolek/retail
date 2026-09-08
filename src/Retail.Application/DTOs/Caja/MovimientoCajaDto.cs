using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Parámetros para registrar un ingreso o egreso extraordinario de efectivo en el turno activo.
/// </summary>
public record class MovimientoCajaDto
{
    public required int IdTurno { get; init; }
    public required TipoMovimientoCajaEnum TipoMovimiento { get; init; }
    public required decimal Monto { get; init; }
    public required string Concepto { get; init; }
}
