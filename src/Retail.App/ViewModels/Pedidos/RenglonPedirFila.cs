using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Common;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Renglón del modal de pedido: un ítem que se puede pedir, con su stock para decidir la cantidad (A-17) y la
/// cantidad que ya figura en el borrador, a la que se sumará la nueva.
/// </summary>
public partial class RenglonPedirFila : ObservableObject
{
    public RenglonPedirFila(RenglonVistaPreviaDto renglon)
    {
        Renglon = renglon ?? throw new ArgumentNullException(nameof(renglon));
    }

    public RenglonVistaPreviaDto Renglon { get; }

    public int IdCatalogoProveedor => Renglon.IdCatalogoProveedor;

    public string CodigoProveedor => Renglon.CodigoProveedor;

    public string Descripcion => Renglon.Descripcion;

    public decimal CostoActual => Renglon.CostoActual;

    public int StockActual => Renglon.StockActual;

    public int StockMinimo => Renglon.StockMinimo;

    /// <summary>Mismo criterio que la alerta de stock mínimo del catálogo (RF-08).</summary>
    public bool StockBajo => StockActual <= StockMinimo;

    public int CantidadEnBorrador => Renglon.CantidadEnBorrador;

    public bool TieneCantidadEnBorrador => CantidadEnBorrador > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsValida))]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad = 1;

    public bool EsValida => Cantidad >= 1;

    public decimal Subtotal => ReglasMonetarias.Redondear(Cantidad * CostoActual);
}
