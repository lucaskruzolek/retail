using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de un artículo para consulta y administración de inventario.
/// </summary>
public record class ArticuloDto : BaseDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public int? IdCategoria { get; init; }
    public string? CategoriaNombre { get; init; }
    public int? IdMarca { get; init; }
    public string? MarcaNombre { get; init; }
    public int? IdCatalogoProveedor { get; init; }
    public string? ProveedorNombre { get; init; }
    public string? CodigoProveedor { get; init; }
    public string? DescripcionProveedor { get; init; }
    public decimal? CostoCatalogoProveedor { get; init; }
    public bool EstaVinculadoAProveedor => IdCatalogoProveedor.HasValue && IdCatalogoProveedor.Value > 0;
    public required decimal CostoReposicion { get; init; }
    public required decimal PorcentajeGanancia { get; init; }
    public required decimal PrecioVenta { get; init; }
    public required int StockActual { get; init; }
    public required int StockMinimo { get; init; }
    public required bool EsServicio { get; init; }
    public bool StockBajo => !EsServicio && StockActual <= StockMinimo;
}
