namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Por qué el sistema sugiere una categoría o una marca (RF-27: "indica el motivo de cada sugerencia"). El orden de los
/// valores sigue la precedencia de M-14: primero lo que informó el proveedor y después los diccionarios. Toda sugerencia
/// sale de una regla aprobada por una persona (M-45, M-49).
/// </summary>
public enum MotivoSugerenciaEnum
{
    /// <summary>
    /// Una equivalencia registrada para este proveedor traduce su rubro, categoría o marca (M-13).
    /// </summary>
    EquivalenciaDelProveedor = 1,

    /// <summary>
    /// La columna de marca de la planilla coincide con el nombre o un alias de una marca del comercio (sección 4.1).
    /// </summary>
    MarcaInformadaPorElProveedor = 2,

    /// <summary>
    /// La primera palabra de la descripción está en la tabla de términos y no es ambigua.
    /// </summary>
    Termino = 3,

    /// <summary>
    /// La descripción nombra la marca o uno de sus alias.
    /// </summary>
    MarcaEnLaDescripcion = 4,

    // El valor 5 (prefijo del código de barras) se retiró con M-69: el prefijo solo desempata entre marcas que nombra
    // el texto, y entonces el motivo es MarcaEnLaDescripcion.

    /// <summary>
    /// El término es ambiguo y el par "término + segunda palabra" del diccionario sugiere la categoría (Fase 5, M-48).
    /// </summary>
    ParDelDiccionario = 6
}
