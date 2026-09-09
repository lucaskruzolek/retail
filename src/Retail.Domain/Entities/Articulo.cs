using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un artículo comercial, producto artesanal o servicio en el catálogo.
/// </summary>
public class Articulo : BaseEntity, IAggregateRoot
{
    public string? CodigoBarras { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public int IdCategoria { get; set; }

    public Categoria? Categoria { get; set; }

    public int IdMarca { get; set; }

    public Marca? Marca { get; set; }

    public int? IdCatalogoProveedor { get; set; }

    public CatalogoProveedor? CatalogoProveedor { get; set; }

    public decimal CostoReposicion { get; set; }

    public decimal PorcentajeGanancia { get; set; }

    public decimal PrecioVenta { get; set; }

    public int StockActual { get; set; }

    public int StockMinimo { get; set; }

    public bool EsServicio { get; set; }

    public ICollection<DetalleVenta> DetallesVentas { get; set; } = new List<DetalleVenta>();

    public ICollection<DetallePresupuesto> DetallesPresupuestos { get; set; } = new List<DetallePresupuesto>();

    public ICollection<DetalleCompra> DetallesCompras { get; set; } = new List<DetalleCompra>();

    public void ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)
    {
        CostoReposicion = nuevoCosto;
        PrecioVenta = Math.Round(CostoReposicion * (1m + (PorcentajeGanancia / 100m)), 2);
    }
}
