namespace Retail.App.ViewModels.Comun;

/// <summary>
/// Opciones de "filas por página" compartidas por los listados paginados (Artículos, Clientes, Pedidos, Proveedores y
/// Usuarios). Se define en un solo lugar para que todas las grillas ofrezcan los mismos tamaños; el tamaño inicial de cada
/// ViewModel debe ser uno de estos valores.
/// </summary>
public static class OpcionesPaginacion
{
    /// <summary>Tamaños de página disponibles en el selector de la barra de paginación.</summary>
    public static IReadOnlyList<int> Tamanos { get; } = [15, 30, 60];
}
