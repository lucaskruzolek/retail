using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Registro de cobranza de deuda de cuenta corriente con imputación en turno de caja y saldo de cliente.
/// </summary>
public class CobranzaCliente : BaseEntity
{
    public int IdCliente { get; set; }

    public Cliente? Cliente { get; set; }

    public int IdTurno { get; set; }

    public TurnoCaja? TurnoCaja { get; set; }

    public int IdUsuario { get; set; }

    public Usuario? Usuario { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    public MedioPagoEnum MedioPago { get; set; } = MedioPagoEnum.Efectivo;

    public decimal Monto { get; set; }

    public string? Referencia { get; set; }
}
