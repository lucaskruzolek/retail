namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Renglón de artículo cotizado con su precio unitario pactado congelado por 15 días.
/// </summary>
public record class DetallePresupuestoDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public required int Cantidad { get; init; }
    public required decimal PrecioUnitarioPactado { get; init; }
    public decimal SubtotalItem => Cantidad * PrecioUnitarioPactado;
}
