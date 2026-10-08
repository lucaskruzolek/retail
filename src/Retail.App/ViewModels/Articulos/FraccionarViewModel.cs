using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel del diálogo de fraccionamiento: abre unidades del artículo de origen (pack) y las suma a
/// una presentación derivada (RF-21). Muestra el antes y el después de los dos stocks.
/// </summary>
public partial class FraccionarViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idArticuloDerivado;

    [ObservableProperty]
    private string _descripcionDerivado = string.Empty;

    [ObservableProperty]
    private int _stockDerivado;

    [ObservableProperty]
    private int _unidadesPorOrigen = 1;

    [ObservableProperty]
    private string _descripcionOrigen = string.Empty;

    [ObservableProperty]
    private int _stockOrigen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnidadesResultantes))]
    [NotifyPropertyChangedFor(nameof(StockOrigenResultante))]
    [NotifyPropertyChangedFor(nameof(StockDerivadoResultante))]
    private int _cantidadOrigen = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>
    /// Unidades que recibirá la presentación. Con una cantidad inválida muestra cero.
    /// </summary>
    public int UnidadesResultantes => CantidadOrigen > 0 ? CantidadOrigen * UnidadesPorOrigen : 0;

    public int StockOrigenResultante => StockOrigen - Math.Max(CantidadOrigen, 0);

    public int StockDerivadoResultante => StockDerivado + UnidadesResultantes;

    /// <summary>
    /// Confirma el fraccionamiento en el servicio y devuelve las unidades obtenidas.
    /// </summary>
    public Func<Task<int>>? OnConfirmarAsync { get; set; }

    public int? UnidadesObtenidas { get; set; }

    public void Configurar(ArticuloDto derivado, ArticuloDto origen)
    {
        ArgumentNullException.ThrowIfNull(derivado);
        ArgumentNullException.ThrowIfNull(origen);

        IdArticuloDerivado = derivado.IdArticulo;
        DescripcionDerivado = derivado.Descripcion;
        StockDerivado = derivado.StockActual;
        UnidadesPorOrigen = derivado.UnidadesPorOrigen ?? 1;
        DescripcionOrigen = origen.Descripcion;
        StockOrigen = origen.StockActual;
        CantidadOrigen = 1;
        MensajeError = null;
        IsBusy = false;
        OnConfirmarAsync = null;
        UnidadesObtenidas = null;

        OnPropertyChanged(nameof(UnidadesResultantes));
        OnPropertyChanged(nameof(StockOrigenResultante));
        OnPropertyChanged(nameof(StockDerivadoResultante));
    }

    /// <summary>
    /// Aviso temprano en el diálogo. La regla la vuelve a validar el Dominio (StockInsuficienteException),
    /// porque el stock pudo cambiar desde que se abrió la pantalla.
    /// </summary>
    public bool Validar()
    {
        MensajeError = null;

        if (CantidadOrigen < 1)
        {
            MensajeError = "La cantidad a fraccionar debe ser al menos 1.";
            return false;
        }

        if (CantidadOrigen > StockOrigen)
        {
            MensajeError = $"El artículo de origen solo tiene {StockOrigen} unidad(es) en stock.";
            return false;
        }

        return true;
    }

    public FraccionarDto ObtenerDto() => new()
    {
        IdArticuloDerivado = IdArticuloDerivado,
        CantidadOrigen = CantidadOrigen
    };
}
