namespace Retail.Application.DTOs.Compras;

/// <summary>
/// Renglón de artículo adquirido en factura o remito de distribuidor.
/// </summary>
public record class DetalleCompraDto
{
    public required int IdArticulo { get; init; }
    public required int Cantidad { get; init; }
    public required decimal CostoUnitario { get; init; }
    public decimal SubtotalItem => Cantidad * CostoUnitario;
}
