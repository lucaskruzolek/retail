namespace Retail.Domain.Enums;

/// <summary>
/// Cómo se asignó la categoría o la marca de un artículo (Track 6.6, sección 8.2 y M-5). Toda asignación la confirma
/// una persona: el origen distingue si partió de una sugerencia del sistema, y permite entrenar el modelo solo con
/// datos vistos por alguien y medir cuántas sugerencias se aceptan o se corrigen.
/// </summary>
public enum OrigenClasificacionEnum
{
    /// <summary>
    /// La eligió una persona sin sugerencia del sistema (alta o edición manual, o un dato anterior al Track 6.6).
    /// </summary>
    Manual = 1,

    /// <summary>
    /// El sistema la sugirió y una persona la aceptó sin cambios.
    /// </summary>
    SugerenciaAceptada = 2,

    /// <summary>
    /// El sistema sugirió otra y una persona la corrigió.
    /// </summary>
    SugerenciaCorregida = 3
}
