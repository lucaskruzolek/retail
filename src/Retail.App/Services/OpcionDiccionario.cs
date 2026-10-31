namespace Retail.App.Services;

/// <summary>
/// Opción de una lista desplegable (formularios de diccionario, categorías y marcas del modal de incorporación).
/// <see cref="Id"/> nulo representa la opción "sin …": sin destino (término ambiguo o par ignorado), sin clasificar
/// o sin marca.
/// </summary>
public sealed record OpcionDiccionario(int? Id, string Nombre)
{
    /// <summary>
    /// Nadie decidió todavía (modal de incorporación, M-70): es la selección inicial de los combos de categoría y
    /// marca, y se guarda sin origen.
    /// </summary>
    public static OpcionDiccionario SinClasificar { get; } = new(null, "Sin clasificar");

    /// <summary>
    /// La persona confirmó que el producto no tiene marca (M-7, D-18): se guarda con origen.
    /// </summary>
    public static OpcionDiccionario SinMarca { get; } = new(null, "Sin marca");
}
