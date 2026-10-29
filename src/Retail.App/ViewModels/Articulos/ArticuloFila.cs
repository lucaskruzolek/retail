using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Fila de la grilla de Artículos: el artículo tal como lo devuelve la consulta, más el estado de la pantalla (si
/// está marcado para pedir). La selección vive aquí y no en <see cref="ArticuloDto"/>, que es un contrato de la capa
/// de Aplicación y no debe cargar estado de WPF (L2a-2).
/// </summary>
public partial class ArticuloFila : ObservableObject
{
    public ArticuloFila(ArticuloDto articulo)
    {
        Articulo = articulo ?? throw new ArgumentNullException(nameof(articulo));
        MotivoNoSePuedePedir = DeterminarMotivoNoSePuedePedir(articulo);
    }

    public ArticuloDto Articulo { get; }

    [ObservableProperty]
    private bool _estaSeleccionado;

    /// <summary>
    /// Por qué el artículo no se puede pedir a un proveedor, o <c>null</c> si se puede. Se muestra como tooltip de la
    /// acción deshabilitada y como aviso en el modal de pedido (A-17).
    /// </summary>
    public string? MotivoNoSePuedePedir { get; }

    public bool SePuedePedir => MotivoNoSePuedePedir is null;

    /// <summary>
    /// Ítem de catálogo que se envía al modal de pedido: el que eligió la consulta (la principal o la más barata) o,
    /// si el artículo no vino de ella, la principal (Track 4.3, L4, paso 3c).
    /// </summary>
    public int? IdFuenteParaPedir => Articulo.IdFuentePedido ?? Articulo.IdCatalogoProveedor;

    /// <summary>
    /// Fuentes del artículo además de la principal (Track 4.3, A-19). <see cref="ArticuloDto.CantidadFuentes"/> cuenta
    /// todas, principal incluida; sin principal, todas son adicionales. Nunca es negativo.
    /// </summary>
    public int FuentesAdicionales =>
        Math.Max(0, Articulo.CantidadFuentes - (Articulo.EstaVinculadoAProveedor ? 1 : 0));

    /// <summary>Indica si la grilla debe mostrar el botón "+n" (Track 4.3, A-19).</summary>
    public bool TieneFuentesAdicionales => FuentesAdicionales > 0;

    /// <summary>Texto del botón de fuentes adicionales, por ejemplo "+2" (Track 4.3, A-19).</summary>
    public string TextoFuentesAdicionales => $"+{FuentesAdicionales}";

    /// <summary>
    /// Hay fuentes pero ninguna es la principal: el costo no se actualiza solo y conviene elegirla (Track 4.3, A-19).
    /// </summary>
    public bool TieneFuentesSinPrincipal => !Articulo.EstaVinculadoAProveedor && Articulo.CantidadFuentes > 0;

    /// <summary>Tooltip del botón "+n": aclara que las fuentes se ven y se cambian al abrir el artículo (A-19).</summary>
    public string TooltipFuentes
    {
        get
        {
            string cantidad = FuentesAdicionales == 1 ? "1 fuente adicional" : $"{FuentesAdicionales} fuentes adicionales";
            return $"{cantidad}. Abrir el artículo para verlas o cambiar la principal.";
        }
    }

    /// <summary>
    /// El pedido se arma sobre el ítem de catálogo del proveedor (A-8): sin ninguna fuente no hay qué pedir. Alcanza
    /// una adicional, aunque no haya principal (paso 3c). Una presentación derivada nunca tiene fuentes (RF-21), pero
    /// su motivo es otro y vale nombrarlo.
    /// </summary>
    private static string? DeterminarMotivoNoSePuedePedir(ArticuloDto articulo)
    {
        if (articulo.EsServicio)
        {
            return "es un servicio: no se pide a proveedores";
        }

        if (articulo.EsDerivado)
        {
            return "es una presentación derivada: se pide su artículo de origen";
        }

        return (articulo.IdFuentePedido ?? articulo.IdCatalogoProveedor) is null ? "no tiene fuentes" : null;
    }
}
