using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Listas;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Renglón del editor de listas: un artículo y su cantidad, con el precio de catálogo de hoy solo para mostrar (la lista
/// no guarda precios, L-08). Un artículo dado de baja (L-13) se muestra marcado y no suma al total.
/// </summary>
public partial class RenglonListaFila : ObservableObject
{
    public RenglonListaFila(int idArticulo, string? codigoBarras, string descripcion, decimal precioActual, int cantidad, bool estaDisponible)
    {
        IdArticulo = idArticulo;
        CodigoBarras = codigoBarras;
        Descripcion = descripcion ?? throw new ArgumentNullException(nameof(descripcion));
        PrecioActual = precioActual;
        _cantidad = cantidad;
        EstaDisponible = estaDisponible;
    }

    public int IdArticulo { get; }

    public string? CodigoBarras { get; }

    public string Descripcion { get; }

    public decimal PrecioActual { get; }

    /// <summary><c>false</c> si el artículo fue dado de baja: el precio es informativo y el renglón no suma al total.</summary>
    public bool EstaDisponible { get; }

    public bool EstaDadoDeBaja => !EstaDisponible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalQueSuma))]
    private int _cantidad;

    public decimal Subtotal => Cantidad * PrecioActual;

    /// <summary>Lo que el renglón aporta al total de la lista: cero si el artículo no está disponible.</summary>
    public decimal SubtotalQueSuma => EstaDisponible ? Subtotal : 0m;

    public static RenglonListaFila Desde(RenglonListaDto renglon)
    {
        ArgumentNullException.ThrowIfNull(renglon);

        return new RenglonListaFila(
            renglon.IdArticulo,
            renglon.CodigoBarras,
            renglon.Descripcion,
            renglon.PrecioActual,
            renglon.Cantidad,
            renglon.EstaDisponible);
    }

    /// <summary>Un artículo recién encontrado por el buscador entra con cantidad 1 y disponible (la búsqueda solo devuelve activos).</summary>
    public static RenglonListaFila Desde(ArticuloVentaDto articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        return new RenglonListaFila(articulo.IdArticulo, articulo.CodigoBarras, articulo.Descripcion, articulo.PrecioVenta, 1, true);
    }
}
