namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de un artículo para consulta y administración de inventario.
/// </summary>
public record class ArticuloDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public required int IdCategoria { get; init; }
    public string? CategoriaNombre { get; init; }
    public required int IdMarca { get; init; }
    public string? MarcaNombre { get; init; }
    public int? IdCatalogoProveedor { get; init; }
    public required decimal CostoReposicion { get; init; }
    public required decimal PorcentajeGanancia { get; init; }
    public required decimal PrecioVenta { get; init; }
    public required int StockActual { get; init; }
    public required int StockMinimo { get; init; }
    public required bool EsServicio { get; init; }
    public bool StockBajo => !EsServicio && StockActual <= StockMinimo;
}
