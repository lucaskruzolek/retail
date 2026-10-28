namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Renglón de un pedido para la pantalla de detalle. Código, descripción y costo son la foto tomada del catálogo
/// al agregar el renglón (RF-28).
/// </summary>
public record class RenglonPedidoDto
{
    public int IdCatalogoProveedor { get; init; }

    public int IdArticulo { get; init; }

    public string CodigoProveedor { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public int Cantidad { get; init; }

    public decimal CostoPactado { get; init; }

    public decimal Subtotal { get; init; }

    public bool ArticuloDadoDeBaja { get; init; }
}
