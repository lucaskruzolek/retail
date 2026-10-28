namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Pedido de agregar un ítem del catálogo de un proveedor al borrador de ese proveedor (RF-28).
/// </summary>
public record class AgregarAlPedidoDto
{
    public int IdCatalogoProveedor { get; init; }

    public int Cantidad { get; init; }

    public int IdUsuario { get; init; }
}
