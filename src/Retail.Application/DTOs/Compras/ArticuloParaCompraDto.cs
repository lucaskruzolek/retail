namespace Retail.Application.DTOs.Compras;

/// <summary>
/// Artículo que puede comprarse (ni servicio ni presentación derivada, RF-21), con los datos que la pantalla de
/// Compras necesita para anticipar el nuevo precio de venta (RF-19).
/// </summary>
public record class ArticuloParaCompraDto
{
    public required int IdArticulo { get; init; }

    public string? CodigoBarras { get; init; }

    public required string Descripcion { get; init; }

    public required decimal CostoReposicion { get; init; }

    public required decimal PorcentajeGanancia { get; init; }

    public required decimal PrecioVenta { get; init; }
}
