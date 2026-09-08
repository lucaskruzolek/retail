namespace Retail.Application.DTOs.Compras;

/// <summary>
/// Parámetros de entrada para el registro de una compra de distribuidor.
/// Impacta en stock y gatilla el recálculo automático de precios por markup (RF-19).
/// </summary>
public record class CrearCompraDto
{
    public required int IdProveedor { get; init; }
    public required int IdUsuario { get; init; }
    public required string TipoComprobante { get; init; }
    public required string NumeroComprobante { get; init; }
    public required DateTime FechaEmision { get; init; }
    public required IReadOnlyList<DetalleCompraDto> Items { get; init; }
}
