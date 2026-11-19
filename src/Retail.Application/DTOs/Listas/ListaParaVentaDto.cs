using Retail.Application.DTOs.Ventas;

namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Lista recuperada para venderla en el POS: renglones disponibles a precio de catálogo, faltantes de stock y
/// artículos dados de baja (que quedan fuera, L-13).
/// </summary>
public record class ListaParaVentaDto
{
    public required int IdLista { get; init; }

    public required string Nombre { get; init; }

    /// <summary>Solo los renglones cuyo artículo sigue activo.</summary>
    public required IReadOnlyList<RenglonListaParaVentaDto> Renglones { get; init; }

    public required IReadOnlyList<FaltanteStockDto> Faltantes { get; init; }

    /// <summary>Descripciones de los artículos dados de baja, que quedan fuera de <see cref="Renglones"/>.</summary>
    public required IReadOnlyList<string> ArticulosNoDisponibles { get; init; }

    public bool RequiereConfirmacion => Faltantes.Count > 0 || ArticulosNoDisponibles.Count > 0;
}
