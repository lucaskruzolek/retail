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
    /// <param name="precioAnterior">Precio que tenía el renglón antes de cambiar (PE-13), o <c>null</c> si no cambió.</param>
    public RenglonEditableFila(int idArticulo, string? codigoBarras, string descripcion, decimal precioUnitario, int cantidad, bool estaDisponible, decimal? precioAnterior = null)
    {
        _precioAnterior = precioAnterior;
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
    [NotifyPropertyChangedFor(nameof(SubtotalAnterior))]
    [NotifyPropertyChangedFor(nameof(SubtotalAnteriorQueSuma))]
    private int _cantidad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalQueSuma))]
    [NotifyPropertyChangedFor(nameof(PrecioCambio))]
    [NotifyPropertyChangedFor(nameof(SubtotalAnterior))]
    [NotifyPropertyChangedFor(nameof(SubtotalAnteriorQueSuma))]
    private decimal _precioUnitario;

    /// <summary>Precio que tenía el renglón antes de cambiar (el pactado del original o el anterior a "Actualizar precios"); <c>null</c> si no cambió.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrecioCambio))]
    [NotifyPropertyChangedFor(nameof(SubtotalAnterior))]
    [NotifyPropertyChangedFor(nameof(SubtotalAnteriorQueSuma))]
    private decimal? _precioAnterior;

    /// <summary>El precio de hoy difiere del anterior: la grilla muestra el anterior tachado (PE-13).</summary>
    public bool PrecioCambio => PrecioAnterior is { } anterior && anterior != PrecioUnitario;

    public decimal Subtotal => Cantidad * PrecioUnitario;

    /// <summary>Cantidad actual por el precio anterior (el actual si no cambió).</summary>
    public decimal SubtotalAnterior => Cantidad * (PrecioAnterior ?? PrecioUnitario);

    /// <summary>Lo que el renglón aportaría al subtotal a los precios anteriores: cero si el artículo no está disponible.</summary>
    public decimal SubtotalAnteriorQueSuma => EstaDisponible ? SubtotalAnterior : 0m;

    /// <summary>Lo que el renglón aporta al subtotal: cero si el artículo no está disponible.</summary>
    public decimal SubtotalQueSuma => EstaDisponible ? Subtotal : 0m;

    /// <summary>Un artículo recién encontrado por el buscador entra con cantidad 1 y disponible (la búsqueda solo devuelve activos).</summary>
    public static RenglonEditableFila Desde(ArticuloVentaDto articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        return new RenglonEditableFila(articulo.IdArticulo, articulo.CodigoBarras, articulo.Descripcion, articulo.PrecioVenta, 1, true);
    }
}
