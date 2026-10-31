namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Cambio de la cantidad de un renglón de un pedido en borrador (RF-28).
/// </summary>
public record class CambiarCantidadRenglonDto
{
    public int IdPedido { get; init; }

    public int IdCatalogoProveedor { get; init; }

    public int Cantidad { get; init; }
}
