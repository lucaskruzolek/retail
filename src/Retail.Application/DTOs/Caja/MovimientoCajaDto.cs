using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Caja;

/// <summary>
/// Parámetros para registrar un ingreso o egreso extraordinario de efectivo en el turno activo.
/// </summary>
public record class MovimientoCajaDto
{
    public required int IdTurno { get; init; }

    /// <summary>
    /// Quien registra el movimiento. Solo el dueño del turno puede operarlo.
    /// </summary>
    public required int IdUsuario { get; init; }

    public required TipoMovimientoCajaEnum TipoMovimiento { get; init; }
    public required decimal Monto { get; init; }
    public required string Concepto { get; init; }
}
