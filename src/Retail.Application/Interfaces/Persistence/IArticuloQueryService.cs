using Retail.Application.DTOs.Articulos;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato especializado para consultas y paginación del catálogo de artículos con push-down a SQL Server (Ley 8).
/// </summary>
public interface IArticuloQueryService
{
    Task<ArticulosPaginadosDto> ObtenerArticulosPaginadosAsync(ConsultaArticulosDto consulta, CancellationToken cancellationToken = default);
}
