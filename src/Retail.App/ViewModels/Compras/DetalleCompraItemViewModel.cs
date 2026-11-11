using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Compras;
using Retail.Application.DTOs.Pedidos;
using Retail.Domain.Entities;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Compras;

/// <summary>
/// ViewModel que representa un renglón de la compra a un distribuidor. Calcula de forma reactiva el subtotal y el
/// precio de venta que quedaría al registrar la compra (RF-19): usa el markup real del artículo y la misma fórmula
/// del Dominio, de modo que lo que el operador ve es lo que <c>CompraService</c> va a aplicar.
/// </summary>
public partial class DetalleCompraItemViewModel : ObservableObject
{
    private const string SinCodigo = "—";

    [ObservableProperty]
    private int _idArticulo;

    [ObservableProperty]
    private string _codigoArticulo = string.Empty;

    [ObservableProperty]
    private string _descripcionArticulo = string.Empty;

    /// <summary>Cantidad pedida al distribuidor; nula si el renglón se agregó a mano, sin pedido.</summary>
    [ObservableProperty]
    private int? _cantidadPedida;

    /// <summary>Cantidad recibida según el comprobante.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(NuevoPrecioVenta))]
    [NotifyPropertyChangedFor(nameof(CambiaPrecio))]
    [NotifyPropertyChangedFor(nameof(SubePrecio))]
    [NotifyPropertyChangedFor(nameof(BajaPrecio))]
    private decimal _costoUnitario;

    /// <summary>Costo de reposición vigente del artículo, antes de registrar la compra.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CambiaPrecio))]
    private decimal _costoActual;

    /// <summary>Markup vigente del artículo (RF-19).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NuevoPrecioVenta))]
    [NotifyPropertyChangedFor(nameof(SubePrecio))]
    [NotifyPropertyChangedFor(nameof(BajaPrecio))]
    private decimal _porcentajeGanancia;

    /// <summary>Precio de venta vigente del artículo, antes de registrar la compra.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NuevoPrecioVenta))]
    [NotifyPropertyChangedFor(nameof(SubePrecio))]
    [NotifyPropertyChangedFor(nameof(BajaPrecio))]
    private decimal _precioVentaActual;

    /// <summary>
    /// Política global de redondeo del precio (D-38); la asigna la pantalla de compras al crear el renglón, para que el
    /// nuevo precio mostrado sea el que fijará <c>CompraService</c>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NuevoPrecioVenta))]
    [NotifyPropertyChangedFor(nameof(SubePrecio))]
    [NotifyPropertyChangedFor(nameof(BajaPrecio))]
    private RedondeoPrecioEnum _redondeoPrecio = RedondeoPrecioEnum.EnteroMasCercano;

    /// <summary>Subtotal del renglón (costo unitario por cantidad recibida).</summary>
    public decimal Subtotal => CostoUnitario * Cantidad;

    /// <summary>
    /// Precio de venta resultante de aplicar el markup al costo de la factura. Delega en el Dominio, que es la fuente
    /// única de la fórmula y del redondeo. Con un costo no válido (cero o negativo, mientras el operador tipea)
    /// devuelve el precio vigente, porque la compra todavía no puede cambiarlo.
    /// </summary>
    public decimal NuevoPrecioVenta => CostoUnitario <= 0m
        ? PrecioVentaActual
        : Articulo.CalcularPrecioVenta(CostoUnitario, PorcentajeGanancia, RedondeoPrecio);

    /// <summary>Mismo criterio que <c>CompraService</c>: la compra recalcula el precio solo si el costo cambió.</summary>
    public bool CambiaPrecio => CostoUnitario != CostoActual;

    public bool SubePrecio => CambiaPrecio && NuevoPrecioVenta > PrecioVentaActual;

    public bool BajaPrecio => CambiaPrecio && NuevoPrecioVenta < PrecioVentaActual;

    /// <summary>Renglón de un pedido emitido: parte de lo pedido y al costo pactado.</summary>
    public static DetalleCompraItemViewModel DesdeRenglonDePedido(RenglonPedidoParaCompraDto renglon)
    {
        ArgumentNullException.ThrowIfNull(renglon);

        return new DetalleCompraItemViewModel
        {
            IdArticulo = renglon.IdArticulo,
            CodigoArticulo = ElegirCodigo(renglon.CodigoProveedor, renglon.CodigoBarras),
            DescripcionArticulo = renglon.DescripcionArticulo,
            CantidadPedida = renglon.Cantidad,
            Cantidad = renglon.Cantidad,
            CostoUnitario = renglon.CostoPactado,
            CostoActual = renglon.CostoReposicionActual,
            PorcentajeGanancia = renglon.PorcentajeGanancia,
            PrecioVentaActual = renglon.PrecioVentaActual,
        };
    }

    /// <summary>Artículo agregado a mano con el buscador: una unidad al costo de reposición vigente.</summary>
    public static DetalleCompraItemViewModel DesdeArticulo(ArticuloParaCompraDto articulo)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        return new DetalleCompraItemViewModel
        {
            IdArticulo = articulo.IdArticulo,
            CodigoArticulo = ElegirCodigo(articulo.CodigoBarras),
            DescripcionArticulo = articulo.Descripcion,
            CantidadPedida = null,
            Cantidad = 1,
            CostoUnitario = articulo.CostoReposicion,
            CostoActual = articulo.CostoReposicion,
            PorcentajeGanancia = articulo.PorcentajeGanancia,
            PrecioVentaActual = articulo.PrecioVenta,
        };
    }

    /// <summary>El primer código con contenido; nunca el identificador interno del artículo.</summary>
    private static string ElegirCodigo(params string?[] candidatos)
    {
        return candidatos.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? SinCodigo;
    }
}
