using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Criterios de búsqueda y filtrado para el explorador de catálogo de proveedores.
/// </summary>
public record class ConsultaCatalogoProveedorDto
{
    /// <summary>
    /// Valor de <see cref="IdsCategoria"/> que representa "Sin categoría": ítems ya clasificados que ninguna regla
    /// reconoce, o cuya categoría sugerida fue dada de baja (M-74). Los pendientes no cuentan: el motor todavía no los vio.
    /// </summary>
    public const int IdSinCategoria = -1;

    /// <summary>Valor de <see cref="IdsMarca"/> que representa "Sin marca"; misma semántica que <see cref="IdSinCategoria"/>.</summary>
    public const int IdSinMarca = -1;

    public int? IdProveedor { get; init; }

    public string? TerminoBusqueda { get; init; }

    public EstadoVinculacionCatalogoEnum EstadoVinculacion { get; init; } = EstadoVinculacionCatalogoEnum.Todos;

    /// <summary>
    /// Categorías del comercio sugeridas para el ítem (Track 6.6, M-72, M-78); pueden incluir
    /// <see cref="IdSinCategoria"/>. Una lista vacía no filtra; los valores se combinan con O y con los demás filtros
    /// con Y. Los ids positivos solo coinciden con categorías activas.
    /// </summary>
    public IReadOnlyList<int> IdsCategoria { get; init; } = [];

    /// <summary>
    /// Marcas del comercio sugeridas para el ítem (M-72, M-78); pueden incluir <see cref="IdSinMarca"/>. Misma
    /// semántica que <see cref="IdsCategoria"/>.
    /// </summary>
    public IReadOnlyList<int> IdsMarca { get; init; } = [];

    public int Pagina { get; init; } = 1;

    public int TamañoPagina { get; init; } = 50;
}
