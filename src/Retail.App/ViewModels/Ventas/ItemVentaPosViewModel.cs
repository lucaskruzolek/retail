using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// Modelo observable para cada línea de artículo dentro de la grilla de mostrador (POS).
/// Provee formateo monoespaciado contable y reactividad de cantidades y subtotales.
/// </summary>
public partial class ItemVentaPosViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    [ObservableProperty]
    private int _numeroItem;

    [ObservableProperty]
    private int _idArticulo;

    [ObservableProperty]
    private string? _codigoBarras;

    [ObservableProperty]
    private string _descripcion = string.Empty;

    [ObservableProperty]
    private decimal _precioUnitario;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtotalItem))]
    [NotifyPropertyChangedFor(nameof(SubtotalItemFormateado))]
    [NotifyPropertyChangedFor(nameof(TieneAlertaStockBajo))]
    private int _cantidad = 1;

    /// <summary>
    /// D-25: la cantidad se puede tipear en la grilla. Un valor menor a 1 se descarta y vuelve el anterior: quitar un
    /// artículo es una acción explícita (papelera o Supr), no el efecto de un 0 tipeado por error.
    /// </summary>
    partial void OnCantidadChanged(int oldValue, int newValue)
    {
        if (newValue < 1)
        {
            Cantidad = oldValue;
        }
    }

    [ObservableProperty]
    private int _stockActual;

    [ObservableProperty]
    private bool _esServicio;

    public decimal SubtotalItem => Cantidad * PrecioUnitario;

    public string PrecioUnitarioFormateado => PrecioUnitario.ToString("C2", CulturaArgentina);

    public string SubtotalItemFormateado => SubtotalItem.ToString("C2", CulturaArgentina);

    public string IdentificadorVisual => !string.IsNullOrWhiteSpace(CodigoBarras)
        ? CodigoBarras
        : (EsServicio ? "(Servicio)" : "(Artesanal)");

    public bool TieneAlertaStockBajo => !EsServicio && Cantidad > StockActual;
}
