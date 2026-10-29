using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Common;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Renglón del modal de pedido: un artículo que se puede pedir, con su stock para decidir la cantidad (A-17) y la
/// fuente a la que se le pide (A-18). Cambiar la fuente cambia el costo, el código y la cantidad que ya figura en el
/// borrador, a la que se sumará la nueva; el modal mueve el renglón al grupo del proveedor de esa fuente (K3).
/// </summary>
public partial class RenglonPedirFila : ObservableObject
{
    private FuenteRenglonDto _fuente;

    public RenglonPedirFila(RenglonVistaPreviaDto renglon)
    {
        Renglon = renglon ?? throw new ArgumentNullException(nameof(renglon));

        // Una vista previa sin fuentes (un artículo con una sola, armado a mano) se trata como una lista de una.
        Fuentes = renglon.Fuentes.Count > 0 ? renglon.Fuentes : [FuenteDelRenglon(renglon)];
        _fuente = Fuentes.FirstOrDefault(f => f.IdCatalogoProveedor == renglon.IdCatalogoProveedor) ?? Fuentes[0];
    }

    public RenglonVistaPreviaDto Renglon { get; }

    /// <summary>Las fuentes del artículo, ordenadas por costo (A-18).</summary>
    public IReadOnlyList<FuenteRenglonDto> Fuentes { get; }

    /// <summary>El combo solo se muestra si hay entre qué elegir.</summary>
    public bool TieneVariasFuentes => Fuentes.Count > 1;

    /// <summary>
    /// La fuente elegida. El combo puede enviar un valor nulo mientras se reconstruye: se ignora, para que el renglón
    /// nunca quede sin fuente.
    /// </summary>
    public FuenteRenglonDto Fuente
    {
        get => _fuente;
        set
        {
            if (value is null || !SetProperty(ref _fuente, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IdCatalogoProveedor));
            OnPropertyChanged(nameof(IdProveedor));
            OnPropertyChanged(nameof(CodigoProveedor));
            OnPropertyChanged(nameof(Descripcion));
            OnPropertyChanged(nameof(CostoActual));
            OnPropertyChanged(nameof(CantidadEnBorrador));
            OnPropertyChanged(nameof(TieneCantidadEnBorrador));
            OnPropertyChanged(nameof(Subtotal));
        }
    }

    public int IdCatalogoProveedor => Fuente.IdCatalogoProveedor;

    public int IdProveedor => Fuente.IdProveedor;

    public string CodigoProveedor => Fuente.CodigoProveedor;

    public string Descripcion => Fuente.Descripcion;

    public decimal CostoActual => Fuente.CostoActual;

    public int StockActual => Renglon.StockActual;

    public int StockMinimo => Renglon.StockMinimo;

    /// <summary>Mismo criterio que la alerta de stock mínimo del catálogo (RF-08).</summary>
    public bool StockBajo => StockActual <= StockMinimo;

    public int CantidadEnBorrador => Fuente.CantidadEnBorrador;

    public bool TieneCantidadEnBorrador => CantidadEnBorrador > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsValida))]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad = 1;

    public bool EsValida => Cantidad >= 1;

    public decimal Subtotal => ReglasMonetarias.Redondear(Cantidad * CostoActual);

    private static FuenteRenglonDto FuenteDelRenglon(RenglonVistaPreviaDto renglon)
    {
        return new FuenteRenglonDto
        {
            IdCatalogoProveedor = renglon.IdCatalogoProveedor,
            IdProveedor = renglon.IdProveedor,
            CodigoProveedor = renglon.CodigoProveedor,
            Descripcion = renglon.Descripcion,
            CostoActual = renglon.CostoActual,
            CantidadEnBorrador = renglon.CantidadEnBorrador
        };
    }
}
