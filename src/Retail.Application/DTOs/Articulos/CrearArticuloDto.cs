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
    /// <summary>
    /// La fuente principal (Track 4.3, A-16): tiene que figurar en <see cref="IdsFuentes"/> o ser nula.
    /// </summary>
    public int? IdCatalogoProveedor { get; init; }

    /// <summary>
    /// Estado deseado de las fuentes del artículo (A-19): ítems de catálogo que deben quedar vinculados. El servicio
    /// compara contra la base, vincula los nuevos y libera los ausentes; una lista vacía libera todas las fuentes.
    /// </summary>
    public IReadOnlyList<int> IdsFuentes { get; init; } = [];
    public required decimal CostoReposicion { get; init; }
    public required decimal PorcentajeGanancia { get; init; }
    public required int StockActual { get; init; }
    public required int StockMinimo { get; init; }
    public required bool EsServicio { get; init; }
}
