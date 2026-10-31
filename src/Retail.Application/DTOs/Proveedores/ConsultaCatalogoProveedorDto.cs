using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Criterios de búsqueda y filtrado para el explorador de catálogo de proveedores.
/// </summary>
public record class ConsultaCatalogoProveedorDto
{
    public int? IdProveedor { get; init; }

    public string? TerminoBusqueda { get; init; }

    public EstadoVinculacionCatalogoEnum EstadoVinculacion { get; init; } = EstadoVinculacionCatalogoEnum.Todos;

    /// <summary>
    /// Categoría del comercio sugerida para el ítem (Track 6.6, M-72). Solo coincide con categorías activas.
    /// </summary>
    public int? IdCategoria { get; init; }

    /// <summary>
    /// Marca del comercio sugerida para el ítem (M-72). Solo coincide con marcas activas.
    /// </summary>
    public int? IdMarca { get; init; }

    /// <summary>
    /// Ítems ya clasificados que ninguna regla reconoce, o cuya categoría sugerida fue dada de baja (M-74). Los
    /// pendientes no cuentan: el motor todavía no los vio. Incompatible con <see cref="IdCategoria"/>.
    /// </summary>
    public bool SoloSinCategoria { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamañoPagina { get; init; } = 50;
}
