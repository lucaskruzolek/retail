namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Precio y stock vigentes de un artículo del ticket, con los datos de su artículo de origen si es una
/// presentación derivada (RF-21). Proyección de solo lectura para verificar el ticket antes de cobrar.
/// </summary>
public record class DisponibilidadArticuloDto
{
    public required int IdArticulo { get; init; }
    public required string Descripcion { get; init; }
    public required decimal PrecioVenta { get; init; }
    public required int StockActual { get; init; }
    public required bool EsServicio { get; init; }
    public int? IdArticuloOrigen { get; init; }
    public string? DescripcionOrigen { get; init; }
    public int? UnidadesPorOrigen { get; init; }
    public int? StockOrigen { get; init; }
}
