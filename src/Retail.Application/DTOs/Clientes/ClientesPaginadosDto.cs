using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Resultado paginado de la consulta del padrón de clientes con métricas de cartera y push-down a SQL Server (Ley 8).
/// </summary>
public record class ClientesPaginadosDto : BaseDto
{
    public IReadOnlyList<ClienteDto> Items { get; init; } = Array.Empty<ClienteDto>();

    public int TotalRegistros { get; init; }

    public int TotalClientes { get; init; }

    public int TotalClientesConDeuda { get; init; }

    public decimal TotalDeudaCartera { get; init; }

    public int PaginaActual { get; init; }

    public int TamanoPagina { get; init; }

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling((double)TotalRegistros / Math.Max(1, TamanoPagina)));
}
