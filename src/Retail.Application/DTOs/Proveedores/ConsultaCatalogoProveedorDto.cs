using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Criterios de búsqueda y filtrado para el explorador de catálogo de proveedores.
/// </summary>
public record class ConsultaCatalogoProveedorDto
{
    public required int IdProveedor { get; init; }
    
    public string? TerminoBusqueda { get; init; }
    
    public EstadoVinculacionCatalogoEnum EstadoVinculacion { get; init; } = EstadoVinculacionCatalogoEnum.Todos;
    
    public int Pagina { get; init; } = 1;
    
    public int TamañoPagina { get; init; } = 50;
}
