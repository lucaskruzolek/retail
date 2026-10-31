namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Borrador al que irán los renglones de un proveedor (A-7). Si <see cref="IdBorrador"/> es nulo, el proveedor no
/// tiene borrador y se creará uno al confirmar.
/// </summary>
public record class DestinoPedidoDto
{
    public int IdProveedor { get; init; }

    public string RazonSocialProveedor { get; init; } = string.Empty;

    public int? IdBorrador { get; init; }

    public int CantidadRenglonesBorrador { get; init; }
}
