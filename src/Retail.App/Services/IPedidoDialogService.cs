using Retail.App.ViewModels.Pedidos;
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
    /// Abre el modal de pedido con la vista previa del lote (A-17). Devuelve la acción elegida: cancelar, confirmar
    /// (con los renglones y su cantidad) o incorporar primero, que solo se ofrece si <paramref name="ofrecerIncorporar"/>.
    /// </summary>
    /// <param name="avisosPrevios">Lo que la pantalla de origen ya excluyó, con su motivo.</param>
    RespuestaPedirArticulos MostrarPedirArticulos(
        VistaPreviaPedidoDto vistaPrevia,
        IReadOnlyList<string> avisosPrevios,
        bool ofrecerIncorporar);
}
