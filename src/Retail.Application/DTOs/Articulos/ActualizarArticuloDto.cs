namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para la modificación de datos y precios de un artículo existente.
/// </summary>
public record class ActualizarArticuloDto
{
    public required int IdArticulo { get; init; }
    public string? CodigoBarras { get; init; }
    public required string Descripcion { get; init; }
    public int? IdCategoria { get; init; }
    public int? IdMarca { get; init; }
    public int? IdCatalogoProveedor { get; init; }
    public required decimal CostoReposicion { get; init; }
    public required decimal PorcentajeGanancia { get; init; }
    public required int StockActual { get; init; }
    public required int StockMinimo { get; init; }
    public required bool EsServicio { get; init; }
}
