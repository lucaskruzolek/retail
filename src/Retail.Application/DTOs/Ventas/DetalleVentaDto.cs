namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Línea de ítem vendido dentro de una operación en mostrador.
/// </summary>
public record class DetalleVentaDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public required int Cantidad { get; init; }
    public required decimal PrecioUnitario { get; init; }
    public decimal SubtotalItem => Cantidad * PrecioUnitario;
}
