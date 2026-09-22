using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Catálogo de precios y códigos de distribución importados desde planillas de proveedores.
/// </summary>
public class CatalogoProveedor : BaseEntity
{
    public int IdProveedor { get; set; }

    public Proveedor? Proveedor { get; set; }

    public string CodigoProveedor { get; set; } = string.Empty;

    public string? CodigoBarras { get; set; }

    public string DescripcionProveedor { get; set; } = string.Empty;

    public decimal CostoReposicion { get; set; }

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public ICollection<Articulo> Articulos { get; set; } = new List<Articulo>();

    public void ActualizarPrecio(decimal nuevoCosto, string? descripcion = null, string? codigoBarras = null)
    {
        if (nuevoCosto <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(nuevoCosto), "El costo de reposición debe ser estrictamente positivo.");
        }

        CostoReposicion = nuevoCosto;
        FechaActualizacion = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            DescripcionProveedor = descripcion.Trim();
        }

        if (!string.IsNullOrWhiteSpace(codigoBarras))
        {
            CodigoBarras = codigoBarras.Trim();
        }
    }
}
