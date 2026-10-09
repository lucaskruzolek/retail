namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Artículo del ticket cuyo stock no alcanza (RF-10). Si es una presentación derivada y su origen tiene stock
/// suficiente, indica cuántas unidades del origen hay que fraccionar para cubrir el faltante (RF-21).
/// </summary>
public record class FaltanteStockDto
{
    public required int IdArticulo { get; init; }
    public required string Descripcion { get; init; }
    public required int CantidadSolicitada { get; init; }
    public required int StockActual { get; init; }
    public int Faltante => CantidadSolicitada - StockActual;
    public string? DescripcionOrigen { get; init; }
    public int? UnidadesPorOrigen { get; init; }
    public int? StockOrigen { get; init; }

    /// <summary>
    /// Mínimo de unidades del origen a fraccionar para cubrir el faltante. 0 si no es una presentación derivada.
    /// </summary>
    public int OrigenesAFraccionar { get; init; }

    public bool PuedeFraccionar => OrigenesAFraccionar > 0 && StockOrigen >= OrigenesAFraccionar;
}
