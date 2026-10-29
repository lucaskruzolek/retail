using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Common;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Renglones del modal de pedido que van al mismo proveedor, con el destino informado (A-7, A-17): el usuario ve a
/// qué borrador irán, pero no lo elige.
/// </summary>
public sealed class GrupoPedidoProveedor : ObservableObject
{
    public GrupoPedidoProveedor(DestinoPedidoDto destino, IEnumerable<RenglonPedirFila> renglones)
    {
        Destino = destino ?? throw new ArgumentNullException(nameof(destino));
        Renglones = new ObservableCollection<RenglonPedirFila>(renglones);
    }

    public DestinoPedidoDto Destino { get; }

    public string RazonSocialProveedor => Destino.RazonSocialProveedor;

    public string TextoDestino => Destino.IdBorrador is { } idBorrador
        ? $"Se agregará al borrador #{idBorrador} ({TextoRenglones(Destino.CantidadRenglonesBorrador)} actualmente)"
        : "Se creará un borrador nuevo";

    public ObservableCollection<RenglonPedirFila> Renglones { get; }

    /// <summary>Se suman los subtotales sin redondear y se redondea una vez (D-06), igual que el pedido.</summary>
    public decimal Subtotal => ReglasMonetarias.Redondear(Renglones.Sum(r => r.Cantidad * r.CostoActual));

    /// <summary>La invoca el modal cuando cambia una cantidad o se quita un renglón del grupo.</summary>
    public void NotificarSubtotal()
    {
        OnPropertyChanged(nameof(Subtotal));
    }

    private static string TextoRenglones(int cantidad)
    {
        return cantidad == 1 ? "1 renglón" : $"{cantidad} renglones";
    }
}
