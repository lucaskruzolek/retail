using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;
using Retail.Domain.Entities;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel del diálogo de alta de una presentación derivada de un artículo de compra (RF-21).
/// Muestra el origen como referencia y calcula en vivo el costo unitario y el precio con las mismas
/// fórmulas del Dominio, sin duplicarlas.
/// </summary>
public partial class PresentacionFormViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idArticuloOrigen;

    [ObservableProperty]
    private string _descripcionOrigen = string.Empty;

    [ObservableProperty]
    private decimal _costoOrigen;

    [ObservableProperty]
    private int _stockOrigen;

    [ObservableProperty]
    private string _descripcion = string.Empty;

    [ObservableProperty]
    private string? _codigoBarras;

    [ObservableProperty]
    private int _unidadesPorOrigen = 1;

    [ObservableProperty]
    private decimal _porcentajeGanancia = 40m;

    [ObservableProperty]
    private int _stockMinimo;

    [ObservableProperty]
    private decimal _costoUnitarioCalculado;

    [ObservableProperty]
    private decimal _precioVentaCalculado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public Func<Task>? OnGuardarAsync { get; set; }

    partial void OnUnidadesPorOrigenChanged(int value)
    {
        RecalcularVistaPrevia();
    }

    partial void OnPorcentajeGananciaChanged(decimal value)
    {
        RecalcularVistaPrevia();
    }

    public void Configurar(ArticuloDto origen)
    {
        ArgumentNullException.ThrowIfNull(origen);

        IdArticuloOrigen = origen.IdArticulo;
        DescripcionOrigen = origen.Descripcion;
        CostoOrigen = origen.CostoReposicion;
        StockOrigen = origen.StockActual;
        Descripcion = $"{origen.Descripcion} (unidad)";
        CodigoBarras = null;
        UnidadesPorOrigen = 1;
        PorcentajeGanancia = origen.PorcentajeGanancia;
        StockMinimo = 0;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;

        RecalcularVistaPrevia();
    }

    /// <summary>
    /// Vista previa del costo y el precio que fijará el Dominio. Con datos inválidos muestra cero en lugar de
    /// lanzar: el error se informa al validar.
    /// </summary>
    public void RecalcularVistaPrevia()
    {
        if (UnidadesPorOrigen < 1 || CostoOrigen < 0m || PorcentajeGanancia < 0m)
        {
            CostoUnitarioCalculado = 0m;
            PrecioVentaCalculado = 0m;
            return;
        }

        CostoUnitarioCalculado = Articulo.CalcularCostoPresentacion(CostoOrigen, UnidadesPorOrigen);
        PrecioVentaCalculado = Articulo.CalcularPrecioVenta(CostoUnitarioCalculado, PorcentajeGanancia);
    }

    public bool Validar()
    {
        MensajeError = null;

        if (string.IsNullOrWhiteSpace(Descripcion) || Descripcion.Trim().Length < 2)
        {
            MensajeError = "La descripción de la presentación debe contener al menos 2 caracteres.";
            return false;
        }

        if (Descripcion.Trim().Length > 200)
        {
            MensajeError = "La descripción de la presentación no puede exceder 200 caracteres.";
            return false;
        }

        if (UnidadesPorOrigen < 1)
        {
            MensajeError = "Las unidades por origen deben ser al menos 1.";
            return false;
        }

        if (PorcentajeGanancia < 0m)
        {
            MensajeError = "El porcentaje de ganancia no puede ser negativo.";
            return false;
        }

        if (StockMinimo < 0)
        {
            MensajeError = "El stock mínimo no puede ser negativo.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(CodigoBarras) && CodigoBarras.Trim().Length > 50)
        {
            MensajeError = "El código de barras no puede exceder 50 caracteres.";
            return false;
        }

        return true;
    }

    public CrearPresentacionDto ObtenerDto() => new()
    {
        IdArticuloOrigen = IdArticuloOrigen,
        UnidadesPorOrigen = UnidadesPorOrigen,
        Descripcion = Descripcion.Trim(),
        CodigoBarras = string.IsNullOrWhiteSpace(CodigoBarras) ? null : CodigoBarras.Trim(),
        PorcentajeGanancia = PorcentajeGanancia,
        StockMinimo = StockMinimo
    };
}
