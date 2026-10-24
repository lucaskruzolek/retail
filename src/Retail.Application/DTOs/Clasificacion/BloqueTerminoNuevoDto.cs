namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Grupo de ítems del lote cuya primera palabra no está en la tabla de términos y que alcanza el tamaño mínimo de M-26.
/// El usuario decide la categoría una sola vez para todo el grupo, y el término se registra para las próximas
/// importaciones (M-16).
/// </summary>
public record class BloqueTerminoNuevoDto
{
    /// <summary>
    /// Término en forma de comparación ("PELOTERO").
    /// </summary>
    public required string Termino { get; init; }

    public required IReadOnlyList<int> IdsCatalogo { get; init; }

    /// <summary>
    /// Categoría que el modelo asigna a la mayoría del grupo, aunque no alcance el umbral por producto; nula sin modelo
    /// (M-37). Su motivo es <see cref="MotivoSugerenciaEnum.Modelo"/> y su confianza, el promedio en esos ítems.
    /// </summary>
    public SugerenciaDto? Propuesta { get; init; }

    /// <summary>
    /// Cuántos ítems del grupo reciben del modelo la categoría propuesta: con <see cref="IdsCatalogo"/> forma la
    /// consistencia que se muestra ("121 de 123").
    /// </summary>
    public int CoincidenConLaPropuesta { get; init; }

    /// <summary>
    /// Algunas descripciones del grupo, para que la decisión sea informada.
    /// </summary>
    public required IReadOnlyList<string> Ejemplos { get; init; }
}
