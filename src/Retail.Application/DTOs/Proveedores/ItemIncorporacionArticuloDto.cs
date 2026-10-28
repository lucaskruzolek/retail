namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Un ítem del catálogo del proveedor a incorporar a la tienda, con su margen y su clasificación confirmada por una
/// persona (Track 6.6, R-7). Lleva también lo que había sugerido el sistema: con eso el servidor deriva el origen de la
/// clasificación (M-34), así que la pantalla no lo declara.
/// </summary>
public record class ItemIncorporacionArticuloDto
{
    public required int IdCatalogo { get; init; }

    public required decimal PorcentajeGanancia { get; init; }

    /// <summary>
    /// Categoría confirmada; nula si el artículo queda sin clasificar.
    /// </summary>
    public int? IdCategoria { get; init; }

    /// <summary>
    /// Categoría que había sugerido el sistema (RF-27); nula si no sugirió ninguna.
    /// </summary>
    public int? IdCategoriaSugerida { get; init; }

    /// <summary>
    /// Marca confirmada. Nula con <see cref="SinMarcaConfirmada"/> es "Sin marca" (Track 6.5, D-18); nula sin
    /// confirmar, el artículo queda con la marca sin clasificar.
    /// </summary>
    public int? IdMarca { get; init; }

    /// <summary>
    /// La persona confirmó que el producto no tiene marca (M-7). Solo vale con <see cref="IdMarca"/> nulo.
    /// </summary>
    public bool SinMarcaConfirmada { get; init; }

    /// <summary>
    /// Marca que había sugerido el sistema; nula si no sugirió ninguna.
    /// </summary>
    public int? IdMarcaSugerida { get; init; }
}
