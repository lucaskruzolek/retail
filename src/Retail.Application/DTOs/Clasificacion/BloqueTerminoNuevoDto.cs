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
    /// Algunas descripciones del grupo, para que la decisión sea informada.
    /// </summary>
    public required IReadOnlyList<string> Ejemplos { get; init; }
}
