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
    /// El pedido se arma sobre el ítem de catálogo del proveedor (A-8): sin ese vínculo no hay qué pedir. Una
    /// presentación derivada nunca está vinculada (RF-21), pero su motivo es otro y vale nombrarlo.
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

        return articulo.EstaVinculadoAProveedor ? null : "no tiene proveedor vinculado";
    }
}
