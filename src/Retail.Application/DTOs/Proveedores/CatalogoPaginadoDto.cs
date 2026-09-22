using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Resultado paginado de consulta al catálogo de proveedores con push-down a SQL Server (Ley 8).
/// </summary>
public record class CatalogoPaginadoDto : BaseDto
{
    public IReadOnlyList<CatalogoProveedorDto> Items { get; init; } = Array.Empty<CatalogoProveedorDto>();
    public int TotalRegistros { get; init; }
    public int PaginaActual { get; init; }
    public int TamanoPagina { get; init; }
    public int TotalPaginas => (int)Math.Ceiling((double)TotalRegistros / Math.Max(1, TamanoPagina));
}
