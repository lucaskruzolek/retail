namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Proyección ultrarrápida optimizada para mostrador (POS), búsqueda incremental y scanner.
/// </summary>
public record class ArticuloVentaDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public required decimal PrecioVenta { get; init; }
    public required int StockActual { get; init; }
    public required bool EsServicio { get; init; }
}
