using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;

namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Renglón del <see cref="EditorRenglonesViewModel"/>: un artículo, su cantidad y su precio unitario. Lo comparten el
/// editor de listas (el precio es el de catálogo de hoy, solo para mostrar: la lista no guarda precios, L-08) y el de
/// presupuestos. Un artículo dado de baja (L-13) se muestra marcado y no suma al subtotal.
/// </summary>
public partial class RenglonEditableFila : ObservableObject
{
    public RenglonEditableFila(int idArticulo, string? codigoBarras, string descripcion, decimal precioUnitario, int cantidad, bool estaDisponible)
    {
        IdArticulo = idArticulo;
        CodigoBarras = codigoBarras;
        Descripcion = descripcion ?? throw new ArgumentNullException(nameof(descripcion));
        _precioUnitario = precioUnitario;
        _cantidad = cantidad;
        EstaDisponible = estaDisponible;
    }

    public int IdArticulo { get; }

    public string? CodigoBarras { get; }

    public string Descripcion { get; }

    /// <summary><c>false</c> si el artículo fue dado de baja: el precio es informativo y el renglón no suma al subtotal.</summary>
    public bool EstaDisponible { get; }

    public bool EstaDadoDeBaja => !EstaDisponible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalQueSuma))]
    private int _cantidad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalQueSuma))]
    private decimal _precioUnitario;

    public decimal Subtotal => Cantidad * PrecioUnitario;

    /// <summary>Lo que el renglón aporta al subtotal: cero si el artículo no está disponible.</summary>
    public decimal SubtotalQueSuma => EstaDisponible ? Subtotal : 0m;

    /// <summary>Un artículo recién encontrado por el buscador entra con cantidad 1 y disponible (la búsqueda solo devuelve activos).</summary>
    public static RenglonEditableFila Desde(ArticuloVentaDto articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        return new RenglonEditableFila(articulo.IdArticulo, articulo.CodigoBarras, articulo.Descripcion, articulo.PrecioVenta, 1, true);
    }
}
