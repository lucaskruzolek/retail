namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Cambio de las cantidades de varios renglones de un pedido en borrador (RF-28). Se aplica todo o nada: si una
/// cantidad no es válida, ningún renglón cambia.
/// </summary>
public record class CambiarCantidadesPedidoDto
{
    public int IdPedido { get; init; }

    public IReadOnlyList<CantidadRenglonDto> Renglones { get; init; } = Array.Empty<CantidadRenglonDto>();
}

/// <summary>La nueva cantidad de un renglón, identificado por su ítem del catálogo del proveedor.</summary>
public record class CantidadRenglonDto
{
    public int IdCatalogoProveedor { get; init; }

    public int Cantidad { get; init; }
}
