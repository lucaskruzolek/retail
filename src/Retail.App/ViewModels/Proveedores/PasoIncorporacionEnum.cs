namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Pasos del modal de incorporación con sugerencias (Track 6.6, P6-3). Los dos primeros se saltean si no tienen
/// contenido.
/// </summary>
public enum PasoIncorporacionEnum
{
    /// <summary>
    /// Traducir los valores de clasificación del proveedor que todavía no tienen equivalencia (M-13).
    /// </summary>
    TraducirValores = 1,

    /// <summary>
    /// Decidir en bloque los términos nuevos del lote (M-26).
    /// </summary>
    TerminosNuevos = 2,

    /// <summary>
    /// Revisar la categoría y la marca de cada artículo antes de incorporarlos.
    /// </summary>
    RevisarArticulos = 3
}
