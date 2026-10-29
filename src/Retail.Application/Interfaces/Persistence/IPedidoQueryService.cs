using Retail.Application.DTOs.Pedidos;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Consultas de solo lectura sobre pedidos a proveedor: proyecciones <c>AsNoTracking</c> a DTOs, evaluadas en SQL
/// Server (Ley 8). Las modificaciones pasan siempre por la raíz <c>Pedido</c> (Ley 2).
/// </summary>
public interface IPedidoQueryService
{
    /// <summary>
    /// Artículo vigente vinculado a cada ítem de catálogo, indexado por el id del ítem. Los ítems sin vincular no
    /// figuran. Es el artículo del que el ítem es fuente, principal o adicional (<c>CatalogoProveedor.IdArticulo</c>,
    /// Track 4.3, L3 y J6).
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> ObtenerArticulosVinculadosAsync(
        IReadOnlyCollection<int> idsCatalogoProveedor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vista previa de un lote antes de confirmarlo (A-17): destino de cada proveedor, ítems que se pueden pedir con
    /// su stock y la cantidad que ya figura en el borrador, e ítems omitidos con su motivo. Los renglones respetan el
    /// orden recibido.
    /// </summary>
    Task<VistaPreviaPedidoDto> ObtenerVistaPreviaAsync(
        IReadOnlyCollection<int> idsCatalogoProveedor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identificador del borrador vigente del proveedor, o <c>null</c> si no tiene (A-7: a lo sumo uno).
    /// </summary>
    Task<int?> ObtenerIdBorradorAsync(int idProveedor, CancellationToken cancellationToken = default);

    Task<PedidoResumenDto?> ObtenerResumenAsync(int idPedido, CancellationToken cancellationToken = default);

    Task<PedidosPaginadosDto> ListarAsync(ConsultaPedidosDto consulta, CancellationToken cancellationToken = default);

    Task<PedidoDetalleDto?> ObtenerDetalleAsync(int idPedido, CancellationToken cancellationToken = default);

    /// <summary>
    /// Solo los pedidos <c>Emitido</c> del proveedor, ordenados por fecha de emisión descendente (§5.2).
    /// </summary>
    Task<IReadOnlyList<PedidoEmitidoDto>> ListarEmitidosPorProveedorAsync(int idProveedor, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>null</c> si el pedido no existe o no está <c>Emitido</c>. Omite los renglones cuyo artículo fue dado de
    /// baja: no se puede comprar un artículo dado de baja (§5.2).
    /// </summary>
    Task<PedidoParaCompraDto?> ObtenerParaCompraAsync(int idPedido, CancellationToken cancellationToken = default);
}
