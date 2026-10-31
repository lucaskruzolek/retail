namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Regla que el sistema propone a partir de los artículos confirmados (Track 6.6, Fase 5, M-49): "8 de 8 SET GEOMETRIA
/// son Geometría". No se guarda: el usuario la acepta (par con categoría) o la descarta (par ignorado, que no se vuelve a
/// proponer), ambas con <c>ITerminoClasificacionService.AsignarParesAsync</c>.
/// </summary>
public record class ParPropuestoDto
{
    public required int IdTermino { get; init; }

    public required string Termino { get; init; }

    public required string SegundaPalabra { get; init; }

    /// <summary>
    /// Categoría de la mayoría de los artículos con el par.
    /// </summary>
    public required int IdCategoria { get; init; }

    public required string NombreCategoria { get; init; }

    /// <summary>
    /// Artículos con el par que tienen la categoría propuesta.
    /// </summary>
    public required int Coincidencias { get; init; }

    /// <summary>
    /// Artículos con el par (el soporte de P-5).
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Hasta tres descripciones de los artículos del comercio que coinciden con la propuesta.
    /// </summary>
    public required IReadOnlyList<string> Ejemplos { get; init; }
}
