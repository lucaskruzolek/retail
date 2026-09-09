using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Ítem adquirido dentro de una compra de proveedor con su costo unitario de reposición.
/// </summary>
public class DetalleCompra : BaseEntity
{
    public int IdCompra { get; set; }

    public Compra? Compra { get; set; }

    public int IdArticulo { get; set; }

    public Articulo? Articulo { get; set; }

    public int Cantidad { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal SubtotalItem { get; set; }
}
