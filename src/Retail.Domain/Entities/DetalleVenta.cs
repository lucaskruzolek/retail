using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Ítem individual vendido dentro de una venta. Entidad interna del agregado <see cref="Venta"/>:
/// solo se crea y se modifica a través de <see cref="Venta.AgregarItem"/>.
/// </summary>
public class DetalleVenta : BaseEntity
{
    public int IdVenta { get; private set; }

    public Venta? Venta { get; private set; }

    public int IdArticulo { get; private set; }

    public Articulo? Articulo { get; private set; }

    public int Cantidad { get; private set; }

    public decimal PrecioUnitario { get; private set; }

    public decimal SubtotalItem { get; private set; }

    protected DetalleVenta() { }

    internal DetalleVenta(int idArticulo, int cantidad, decimal precioUnitario)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo no es válido.");
        }

        if (precioUnitario < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(precioUnitario), "El precio unitario no puede ser negativo.");
        }

        ValidarCantidad(cantidad);

        IdArticulo = idArticulo;
        PrecioUnitario = precioUnitario;
        Cantidad = cantidad;
        SubtotalItem = Cantidad * PrecioUnitario;
    }

    internal void SumarCantidad(int cantidad)
    {
        ValidarCantidad(cantidad);

        Cantidad = checked(Cantidad + cantidad);
        SubtotalItem = Cantidad * PrecioUnitario;
    }

    private static void ValidarCantidad(int cantidad)
    {
        if (cantidad < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad vendida debe ser al menos 1.");
        }
    }
}
