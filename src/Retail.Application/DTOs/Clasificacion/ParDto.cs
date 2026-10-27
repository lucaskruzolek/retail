namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Fila de la pestaña de pares (Track 6.6, Fase 6): el término ambiguo, la segunda palabra en forma de comparación y la
/// categoría que sugiere, con su rubro.
/// </summary>
public record class ParDto
{
    public required int IdTermino { get; init; }

    public required string Termino { get; init; }

    public required string SegundaPalabra { get; init; }

    public int? IdCategoria { get; init; }

    public string? NombreCategoria { get; init; }

    public string? NombreRubro { get; init; }

    /// <summary>
    /// Se deriva igual que en la entidad (P-4): sin categoría, el par está ignorado.
    /// </summary>
    public bool EsIgnorado => IdCategoria is null;
}
