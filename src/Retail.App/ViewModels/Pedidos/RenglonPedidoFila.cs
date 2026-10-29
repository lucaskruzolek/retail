using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Pedidos;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Renglón del panel de detalle de <c>PedidosView</c>. La cantidad es editable en un borrador y se guarda solo con
/// una acción explícita (G3): <see cref="TieneCambios"/> indica cuándo mostrar "Aplicar".
/// </summary>
public partial class RenglonPedidoFila : ObservableObject
{
    public RenglonPedidoFila(RenglonPedidoDto renglon, bool esEditable)
    {
        Renglon = renglon ?? throw new ArgumentNullException(nameof(renglon));
        EsEditable = esEditable;
        _cantidad = renglon.Cantidad;
    }

    public RenglonPedidoDto Renglon { get; }

    public int IdCatalogoProveedor => Renglon.IdCatalogoProveedor;

    public string CodigoProveedor => Renglon.CodigoProveedor;

    public string Descripcion => Renglon.Descripcion;

    public decimal CostoPactado => Renglon.CostoPactado;

    public decimal Subtotal => Renglon.Subtotal;

    public bool ArticuloDadoDeBaja => Renglon.ArticuloDadoDeBaja;

    /// <summary>Solo los renglones de un borrador se pueden editar o quitar (§4.1 del rector).</summary>
    public bool EsEditable { get; }

    /// <summary>Cantidad guardada en la base: la que se restablece si el usuario descarta el cambio.</summary>
    public int CantidadOriginal => Renglon.Cantidad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneCambios))]
    private int _cantidad;

    /// <summary>Hay una cantidad válida distinta de la guardada, lista para aplicar.</summary>
    public bool TieneCambios => EsEditable && Cantidad >= 1 && Cantidad != CantidadOriginal;
}
