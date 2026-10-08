namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Parámetros para crear una presentación derivada de un artículo de compra (RF-21).
/// La categoría y la marca se heredan del origen, el costo se deriva de él y el stock inicial es cero:
/// la presentación solo recibe unidades mediante fraccionamiento.
/// </summary>
public record class CrearPresentacionDto
{
    public required int IdArticuloOrigen { get; init; }
    public required int UnidadesPorOrigen { get; init; }
    public required string Descripcion { get; init; }
    public string? CodigoBarras { get; init; }
    public required decimal PorcentajeGanancia { get; init; }
    public required int StockMinimo { get; init; }
}
