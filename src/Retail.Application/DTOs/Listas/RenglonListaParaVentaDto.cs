namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Renglón de una lista listo para cargarse en el ticket del POS: siempre con el precio de catálogo de hoy (L-08).
/// </summary>
public record class RenglonListaParaVentaDto
{
    public required int IdArticulo { get; init; }

    public string? CodigoBarras { get; init; }

    public required string Descripcion { get; init; }

    public required int Cantidad { get; init; }

    public required decimal PrecioActual { get; init; }

    public required bool EsServicio { get; init; }

    public required int StockActual { get; init; }
}
