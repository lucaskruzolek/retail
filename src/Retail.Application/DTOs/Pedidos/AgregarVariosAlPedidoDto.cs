namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Pedido de agregar uno o más ítems del catálogo de proveedores a sus borradores (RF-29, A-17). Cada renglón va al
/// borrador del proveedor de su ítem: el destino no se elige (A-7). Pedir una sola fila es un lote de uno.
/// </summary>
public record class AgregarVariosAlPedidoDto
{
    public int IdUsuario { get; init; }

    public IReadOnlyList<RenglonAAgregarDto> Renglones { get; init; } = Array.Empty<RenglonAAgregarDto>();
}
