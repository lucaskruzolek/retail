namespace Retail.Application.DTOs.Compras;

/// <summary>
/// Comprobante de resultado del ingreso de compra.
/// </summary>
public record class CompraResponseDto
{
    public required int IdCompra { get; init; }
    public required int IdProveedor { get; init; }
    public string? ProveedorRazonSocial { get; init; }
    public required string TipoComprobante { get; init; }
    public required string NumeroComprobante { get; init; }
    public required DateTime FechaEmision { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Total { get; init; }
    public required int ArticulosActualizados { get; init; }
    public required IReadOnlyList<DetalleCompraDto> Items { get; init; }
}
