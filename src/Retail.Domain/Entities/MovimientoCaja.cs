using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Movimiento extraordinario de caja (ingreso justificado o retiro de efectivo).
/// </summary>
public class MovimientoCaja : BaseEntity
{
    public int IdTurno { get; set; }

    public TurnoCaja? TurnoCaja { get; set; }

    public TipoMovimientoCajaEnum TipoMovimiento { get; set; } = TipoMovimientoCajaEnum.IngresoExtraordinario;

    public decimal Monto { get; set; }

    public string Concepto { get; set; } = string.Empty;

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
}
