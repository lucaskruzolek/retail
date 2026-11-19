using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Renglón de una lista de artículos. Entidad interna del agregado <see cref="ListaArticulos"/>: no tiene
/// repositorio (Ley 2) y solo se crea y se modifica a través de la raíz.
/// </summary>
/// <remarks>
/// No guarda descripción ni precio (decisión L-12): a diferencia de <see cref="DetallePresupuesto"/>, que congela
/// una foto de lo cotizado, la lista siempre refleja el catálogo vigente.
/// </remarks>
public class RenglonLista : BaseEntity
{
    public int IdListaArticulos { get; private set; }

    public ListaArticulos? ListaArticulos { get; private set; }

    public int IdArticulo { get; private set; }

    public Articulo? Articulo { get; private set; }

    public int Cantidad { get; private set; }

    protected RenglonLista() { }

    internal RenglonLista(int idArticulo, int cantidad)
    {
        if (idArticulo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idArticulo), "El identificador del artículo no es válido.");
        }

        ExigirCantidad(cantidad);

        IdArticulo = idArticulo;
        Cantidad = cantidad;
    }

    internal void SumarCantidad(int cantidad)
    {
        ExigirCantidad(cantidad);
        Cantidad += cantidad;
    }

    internal void CambiarCantidad(int cantidad)
    {
        ExigirCantidad(cantidad);
        Cantidad = cantidad;
    }

    private static void ExigirCantidad(int cantidad)
    {
        if (cantidad < 1)
        {
            throw new DomainException("La cantidad debe ser al menos 1.");
        }
    }
}
