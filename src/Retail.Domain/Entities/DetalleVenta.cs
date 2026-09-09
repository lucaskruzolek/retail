using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Ítem individual vendido dentro de una venta.
/// </summary>
public class DetalleVenta : BaseEntity
{
    public int IdVenta { get; set; }

    public Venta? Venta { get; set; }

    public int IdArticulo { get; set; }

    public Articulo? Articulo { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal SubtotalItem { get; set; }
}
