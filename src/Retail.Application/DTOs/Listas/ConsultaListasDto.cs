namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Criterios de la grilla de listas con paginación en servidor (Ley 8).
/// </summary>
public record class ConsultaListasDto
{
    /// <summary>Busca por nombre de la lista.</summary>
    public string? TerminoBusqueda { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 20;
}
