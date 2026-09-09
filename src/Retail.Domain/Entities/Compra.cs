using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa la adquisición de mercadería a un proveedor con impacto en costos y stock.
/// </summary>
public class Compra : BaseEntity, IAggregateRoot
{
    public int IdProveedor { get; set; }

    public Proveedor? Proveedor { get; set; }

    public int IdUsuario { get; set; }

    public Usuario? Usuario { get; set; }

    public string TipoComprobante { get; set; } = string.Empty;

    public string NumeroComprobante { get; set; } = string.Empty;

    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }

    public string Estado { get; set; } = "INGRESADA";

    public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
}
