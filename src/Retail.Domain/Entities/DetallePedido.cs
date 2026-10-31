using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Renglón de un pedido a proveedor. Entidad interna del agregado <see cref="Pedido"/>: solo se crea y se
/// modifica a través de <see cref="Pedido.AgregarRenglon"/>.
/// </summary>
/// <remarks>
/// El código del proveedor, la descripción y el costo pactado son una foto del catálogo del proveedor al agregar
/// el renglón: si el catálogo cambia después, el pedido conserva lo que se acordó.
/// </remarks>
public class DetallePedido : BaseEntity
{
    public int IdPedido { get; private set; }

    public Pedido? Pedido { get; private set; }

    public int IdArticulo { get; private set; }

    public Articulo? Articulo { get; private set; }

    public int IdCatalogoProveedor { get; private set; }

    public CatalogoProveedor? CatalogoProveedor { get; private set; }

    public string CodigoProveedor { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;

    public int Cantidad { get; private set; }

    public decimal CostoPactado { get; private set; }

    /// <summary>
    /// Cantidad por costo pactado, sin redondear. Es calculado: no se persiste.
    /// </summary>
    public decimal SubtotalEstimado => Cantidad * CostoPactado;

    protected DetallePedido() { }

    internal DetallePedido(
        int idArticulo,
        int idCatalogoProveedor,
        string codigoProveedor,
        string descripcion,
        int cantidad,
        decimal costoPactado)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo no es válido.");
        }

        if (idCatalogoProveedor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCatalogoProveedor), "El identificador del ítem de catálogo no es válido.");
        }

        IdArticulo = idArticulo;
        IdCatalogoProveedor = idCatalogoProveedor;
        CodigoProveedor = codigoProveedor;
        Descripcion = descripcion;
        Cantidad = cantidad;
        CostoPactado = costoPactado;
    }

    internal void ActualizarFoto(string codigoProveedor, string descripcion, decimal costoPactado)
    {
        CodigoProveedor = codigoProveedor;
        Descripcion = descripcion;
        CostoPactado = costoPactado;
    }

    internal void SumarCantidad(int cantidad)
    {
        Cantidad = checked(Cantidad + cantidad);
    }

    internal void CambiarCantidad(int cantidad)
    {
        Cantidad = cantidad;
    }
}
