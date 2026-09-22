using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Clientes;

/// <summary>
/// Criterios de búsqueda y filtrado para el padrón de clientes con paginación en servidor (Ley 8).
/// </summary>
public record class ConsultaClientesDto
{
    public string? TerminoBusqueda { get; init; }

    public CondicionIvaEnum? CondicionIva { get; init; }

    public bool SoloConDeuda { get; init; }

    public bool SoloConCuentaCorriente { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 10;
}
