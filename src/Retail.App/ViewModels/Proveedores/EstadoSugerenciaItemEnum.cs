namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Estado de la clasificación de un ítem en la grilla del modal de incorporación (Track 6.6, P6-8): cuánto coincide lo
/// que eligió la persona con lo que sugirió el sistema. Es solo de presentación; el origen del artículo lo deriva el
/// servidor (M-34).
/// </summary>
public enum EstadoSugerenciaItemEnum
{
    /// <summary>
    /// El sistema no sugirió nada y la persona todavía no eligió nada.
    /// </summary>
    SinSugerencia = 0,

    /// <summary>
    /// Lo elegido coincide con lo sugerido.
    /// </summary>
    Sugerida = 1,

    /// <summary>
    /// La persona eligió algo distinto de lo sugerido (o eligió algo donde no había sugerencia).
    /// </summary>
    Corregida = 2,

    /// <summary>
    /// La persona confirmó que el producto no tiene marca; la categoría coincide con la sugerida.
    /// </summary>
    SinMarca = 3
}
