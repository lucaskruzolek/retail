using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Compras;

/// <summary>
/// ViewModel que representa una línea individual de ítem dentro de la factura de compra a distribuidor.
/// Calcula de manera reactiva el subtotal y el nuevo precio de venta sugerido por markup (RF-19).
/// </summary>
public partial class DetalleCompraItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idArticulo;

    [ObservableProperty]
    private string _codigoArticulo = string.Empty;

    [ObservableProperty]
    private string _descripcionArticulo = string.Empty;

    [ObservableProperty]
    private decimal _costoUnitario;

    [ObservableProperty]
    private int _cantidad = 1;

    [ObservableProperty]
    private decimal _porcentajeGanancia = 40m; // Margen o markup comercial por defecto

    /// <summary>
    /// Subtotal del renglón (Costo Unitario * Cantidad).
    /// </summary>
    public decimal Subtotal => CostoUnitario * Cantidad;

    /// <summary>
    /// Previsualización inmediata del nuevo precio de venta sugerido: CostoReposicion * (1 + (Markup / 100))[cite: 8].
    /// </summary>
    public decimal NuevoPrecioVentaSugerido => CostoUnitario * (1 + (PorcentajeGanancia / 100m));

    partial void OnCostoUnitarioChanged(decimal value)
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(NuevoPrecioVentaSugerido));
    }

    partial void OnCantidadChanged(int value)
    {
        OnPropertyChanged(nameof(Subtotal));
    }
}
