using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Ítem cotizado dentro de un presupuesto con precio pactado congelado.
/// </summary>
public class DetallePresupuesto : BaseEntity
{
    public int IdPresupuesto { get; set; }

    public Presupuesto? Presupuesto { get; set; }

    public int IdArticulo { get; set; }

    public Articulo? Articulo { get; set; }

    public int Cantidad { get; set; }

    public decimal PrecioUnitarioPactado { get; set; }

    public decimal SubtotalItem { get; set; }
}
