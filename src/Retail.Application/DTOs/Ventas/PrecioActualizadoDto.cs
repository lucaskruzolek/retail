namespace Retail.Application.DTOs.Ventas;

/// <summary>
/// Artículo del ticket cuyo precio cambió en el catálogo desde que se cargó (D-14).
/// </summary>
public record class PrecioActualizadoDto
{
    public required int IdArticulo { get; init; }
    public required string Descripcion { get; init; }
    public required decimal PrecioAnterior { get; init; }
    public required decimal PrecioActual { get; init; }
}
