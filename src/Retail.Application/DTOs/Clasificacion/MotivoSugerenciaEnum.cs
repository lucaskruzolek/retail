namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Por qué el sistema sugiere una categoría o una marca (RF-27: "indicando el motivo"). El orden de los valores sigue
/// la precedencia de M-14: primero lo que informó el proveedor, después los diccionarios y al final el modelo.
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

    /// <summary>
    /// El prefijo del código de barras pertenece a la marca (M-25).
    /// </summary>
    PrefijoDelCodigoDeBarras = 5,

    /// <summary>
    /// El modelo de categorías, con confianza mayor o igual al umbral τ.
    /// </summary>
    Modelo = 6
}
