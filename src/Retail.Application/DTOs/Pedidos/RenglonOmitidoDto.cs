namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Ítem de catálogo que no se agregó (o no se puede agregar) a un pedido, con el motivo. El código y la descripción
/// son nulos si el ítem no existe.
/// </summary>
public record class RenglonOmitidoDto
{
    public int IdCatalogoProveedor { get; init; }

    public string? CodigoProveedor { get; init; }

    public string? Descripcion { get; init; }

    public MotivoOmisionPedidoEnum Motivo { get; init; }
}
