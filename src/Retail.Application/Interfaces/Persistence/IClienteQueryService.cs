using Retail.Application.DTOs.Clientes;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato especializado para consultas, búsqueda rápida y paginación de clientes con push-down a SQL Server (Ley 8).
/// </summary>
public interface IClienteQueryService
{
    Task<ClientesPaginadosDto> ObtenerClientesPaginadosAsync(ConsultaClientesDto consulta, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClienteDto>> BuscarClientesRapidoAsync(string terminoBusqueda, int limite = 50, CancellationToken cancellationToken = default);
}
