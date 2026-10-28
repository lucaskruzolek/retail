namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Registra que un término ambiguo seguido de una segunda palabra sugiere una categoría (Track 6.6, Fase 5, M-48). Si
/// el par ya existe, reemplaza su destino; sin categoría, el par queda ignorado y no se vuelve a proponer (P-4).
/// </summary>
public record class AsignarParDto
{
    public required int IdTermino { get; init; }

    public required string SegundaPalabra { get; init; }

    public int? IdCategoria { get; init; }
}
