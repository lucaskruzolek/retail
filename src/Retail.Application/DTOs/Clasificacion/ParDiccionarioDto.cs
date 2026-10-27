namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Par "término ambiguo + segunda palabra" con categoría (Track 6.6, Fase 5, M-48). El motor no recibe los pares
/// ignorados: para sugerir, ignorado e inexistente son lo mismo (P-4).
/// </summary>
public record class ParDiccionarioDto
{
    public required string Termino { get; init; }

    public required string SegundaPalabra { get; init; }

    public required int IdCategoria { get; init; }
}
