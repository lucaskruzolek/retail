using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Casos de uso del pedido a proveedor (RF-28) y su contrato de frontera con Compras (RF-29).
/// </summary>
public interface IPedidoService
{
    /// <summary>
    /// Contrato de frontera con Compras, §5.2: pedidos emitidos del proveedor, del más reciente al más antiguo.
    /// </summary>
    Task<IReadOnlyList<PedidoEmitidoDto>> ListarEmitidosPorProveedorAsync(int idProveedor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Contrato de frontera con Compras, §5.2: <c>null</c> si el pedido no existe o no está emitido.
    /// </summary>
    Task<PedidoParaCompraDto?> ObtenerParaCompraAsync(int idPedido, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega el ítem al borrador de su proveedor, creándolo si no existe. Devuelve el resumen del pedido.
    /// </summary>
    Task<PedidoResumenDto> AgregarAlPedidoAsync(AgregarAlPedidoDto dto, CancellationToken cancellationToken = default);

    Task<PedidosPaginadosDto> ListarPedidosAsync(ConsultaPedidosDto consulta, CancellationToken cancellationToken = default);

    Task<PedidoDetalleDto?> ObtenerDetalleAsync(int idPedido, CancellationToken cancellationToken = default);

    Task CambiarCantidadAsync(CambiarCantidadRenglonDto dto, CancellationToken cancellationToken = default);

    Task QuitarRenglonAsync(int idPedido, int idCatalogoProveedor, CancellationToken cancellationToken = default);

    Task EmitirAsync(int idPedido, CancellationToken cancellationToken = default);

    Task CancelarAsync(CancelarPedidoDto dto, CancellationToken cancellationToken = default);
}
