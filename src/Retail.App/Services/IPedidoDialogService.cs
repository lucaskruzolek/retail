using Retail.Application.DTOs.Pedidos;

namespace Retail.App.Services;

/// <summary>
/// Diálogos del módulo de pedidos a proveedor (Track 4.3). Desacopla los ViewModels de las ventanas WPF para que
/// sus flujos se prueben sin interfaz.
/// </summary>
public interface IPedidoDialogService
{
    bool Confirmar(string titulo, string mensaje);

    void MostrarAlerta(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);

    /// <summary>
    /// Abre el modal de pedido con la vista previa del lote (A-17). Devuelve los renglones confirmados con su
    /// cantidad, o <c>null</c> si el usuario cancela.
    /// </summary>
    IReadOnlyList<RenglonAAgregarDto>? MostrarPedirArticulos(VistaPreviaPedidoDto vistaPrevia);
}
