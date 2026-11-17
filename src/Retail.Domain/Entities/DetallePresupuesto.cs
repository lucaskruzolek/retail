using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Renglón de un presupuesto. Entidad interna del agregado <see cref="Presupuesto"/>: solo se crea y se modifica
/// a través de <see cref="Presupuesto.Emitir"/>.
/// </summary>
/// <remarks>
/// La descripción y el precio pactado son una foto del artículo al emitir (PS-11): si el artículo se renombra o
/// cambia de precio después, el documento entregado al cliente sigue diciendo lo mismo.
/// </remarks>
public class DetallePresupuesto : BaseEntity
{
    public const int LongitudMaximaDescripcion = 200;

    public int IdPresupuesto { get; private set; }

    public Presupuesto? Presupuesto { get; private set; }

    public int IdArticulo { get; private set; }

    public Articulo? Articulo { get; private set; }

    public string Descripcion { get; private set; } = string.Empty;

    public int Cantidad { get; private set; }

    public decimal PrecioUnitarioPactado { get; private set; }

    /// <summary>
    /// Cantidad por precio pactado, sin redondear. Se persiste y se recalcula cuando cambia la cantidad.
    /// </summary>
    public decimal SubtotalItem { get; private set; }

    protected DetallePresupuesto() { }

    internal DetallePresupuesto(int idArticulo, string descripcion, int cantidad, decimal precioUnitarioPactado)
    {
        IdArticulo = idArticulo;
        Descripcion = descripcion;
        Cantidad = cantidad;
        PrecioUnitarioPactado = precioUnitarioPactado;
        SubtotalItem = cantidad * precioUnitarioPactado;
    }

    internal void SumarCantidad(int cantidad)
    {
        Cantidad = checked(Cantidad + cantidad);
        SubtotalItem = Cantidad * PrecioUnitarioPactado;
    }
}
