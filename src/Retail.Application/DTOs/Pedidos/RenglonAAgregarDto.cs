namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Un ítem de catálogo y la cantidad a pedir dentro de un <see cref="AgregarVariosAlPedidoDto"/>.
/// </summary>
public record class RenglonAAgregarDto
{
    public int IdCatalogoProveedor { get; init; }

    public int Cantidad { get; init; }
}
