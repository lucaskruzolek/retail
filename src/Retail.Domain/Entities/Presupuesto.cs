using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa una cotización comercial temporal con precios pactados congelados.
/// </summary>
public class Presupuesto : BaseEntity, IAggregateRoot
{
    public int IdUsuario { get; set; }

    public Usuario? Usuario { get; set; }

    public int IdCliente { get; set; }

    public Cliente? Cliente { get; set; }

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;

    public DateTime FechaVencimiento { get; set; } = DateTime.UtcNow.AddDays(15);

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal Total { get; set; }

    public EstadoPresupuestoEnum Estado { get; set; } = EstadoPresupuestoEnum.Pendiente;

    public ICollection<DetallePresupuesto> Detalles { get; set; } = new List<DetallePresupuesto>();

    public ICollection<Venta> VentasOriginadas { get; set; } = new List<Venta>();
}
