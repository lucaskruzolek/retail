using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Catálogo de precios y códigos de distribución importados desde planillas de proveedores.
/// </summary>
public class CatalogoProveedor : BaseEntity, IAggregateRoot
{
    public int IdProveedor { get; set; }

    public Proveedor? Proveedor { get; set; }

    public string CodigoProveedor { get; set; } = string.Empty;

    public string DescripcionProveedor { get; set; } = string.Empty;

    public decimal CostoReposicion { get; set; }

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();
}
