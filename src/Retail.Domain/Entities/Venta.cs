using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa una transacción de venta formalizada en el mostrador.
/// </summary>
public class Venta : BaseEntity, IAggregateRoot
{
    public int IdTurno { get; set; }

    public TurnoCaja? TurnoCaja { get; set; }

    public int IdUsuario { get; set; }

    public Usuario? Usuario { get; set; }

    public int IdCliente { get; set; }

    public Cliente? Cliente { get; set; }

    public int? IdPresupuestoOrigen { get; set; }

    public Presupuesto? PresupuestoOrigen { get; set; }

    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal Total { get; set; }

    public EstadoFiscalEnum EstadoFiscal { get; set; } = EstadoFiscalEnum.NoAplica;

    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();

    public ICollection<PagoVenta> Pagos { get; set; } = new List<PagoVenta>();

    public ComprobanteFiscal? ComprobanteFiscal { get; set; }
}
