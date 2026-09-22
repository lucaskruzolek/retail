namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Parámetros para incorporar uno o varios ítems del catálogo de un proveedor a la tabla propia de Artículos.
/// Permite categorías y marcas nulables para artículos no clasificados y porcentajes de ganancia individuales por ítem.
/// </summary>
public record class IncorporarCatalogoArticulosDto
{
    public IReadOnlyList<ItemIncorporacionArticuloDto> Items { get; init; } = Array.Empty<ItemIncorporacionArticuloDto>();

    public IReadOnlyList<int>? IdsCatalogo { get; init; }

    public int? IdCategoria { get; init; }

    public int? IdMarca { get; init; }

    public decimal PorcentajeGananciaSugerido { get; init; } = 40m;
}

