namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Fila de la pestaña de términos (Track 6.6, Fase 6): el término y la categoría que sugiere, con su rubro.
/// </summary>
public record class TerminoDto
{
    public required int IdTermino { get; init; }

    public required string Termino { get; init; }

    public int? IdCategoria { get; init; }

    public string? NombreCategoria { get; init; }

    public string? NombreRubro { get; init; }

    /// <summary>
    /// Se deriva igual que en la entidad (M-27): sin categoría, el término es ambiguo.
    /// </summary>
    public bool EsAmbiguo => IdCategoria is null;
}
