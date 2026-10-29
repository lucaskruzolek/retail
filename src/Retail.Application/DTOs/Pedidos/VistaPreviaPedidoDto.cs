namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Lo que el modal de pedido muestra antes de confirmar un lote (A-17): el destino de cada proveedor, los ítems que
/// se pueden pedir y los que no, con su motivo. Proyección de solo lectura (Ley 8).
/// </summary>
public record class VistaPreviaPedidoDto
{
    public IReadOnlyList<DestinoPedidoDto> Destinos { get; init; } = Array.Empty<DestinoPedidoDto>();

    public IReadOnlyList<RenglonVistaPreviaDto> Renglones { get; init; } = Array.Empty<RenglonVistaPreviaDto>();

    public IReadOnlyList<RenglonOmitidoDto> Omitidos { get; init; } = Array.Empty<RenglonOmitidoDto>();
}
