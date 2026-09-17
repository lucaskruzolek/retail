using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Resumen de artículo con stock en estado crítico (StockActual menor o igual a StockMinimo).
/// </summary>
public record class AlertaStockDto : BaseDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public required int StockActual { get; init; }
    public required int StockMinimo { get; init; }
    public int Deficit => StockMinimo - StockActual;
}
