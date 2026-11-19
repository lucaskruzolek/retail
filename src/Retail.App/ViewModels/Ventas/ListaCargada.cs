namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// Lista de artículos de la que viene el ticket del POS (RF-30): lo que la ficha muestra, el número que el cobro manda al
/// servidor como <c>IdListaOrigen</c> (L-09) y la <c>FechaModificacion</c> con la que se detecta un cambio concurrente si el
/// cajero elige "Actualizar «nombre»" (L-16).
/// </summary>
/// <param name="Id">Número de la lista.</param>
/// <param name="Nombre">Nombre de la lista, tal como se imprime (L-11).</param>
/// <param name="FechaModificacion">Instante UTC de la última modificación que el POS conoce de la lista.</param>
public sealed record ListaCargada(int Id, string Nombre, DateTime FechaModificacion)
{
    /// <summary>Texto de la ficha: «Lista «3.º grado»».</summary>
    public string Etiqueta => $"Lista «{Nombre}»";
}
