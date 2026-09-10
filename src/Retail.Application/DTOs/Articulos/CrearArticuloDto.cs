namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros de entrada para el alta de un nuevo artículo o servicio.
/// Soporta código de barras nulo para productos artesanales o servicios.
/// </summary>
public record class CrearArticuloDto
{
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
