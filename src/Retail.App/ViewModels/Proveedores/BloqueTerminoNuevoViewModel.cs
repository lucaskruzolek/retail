using CommunityToolkit.Mvvm.ComponentModel;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// Tarjeta del paso "Términos nuevos": un grupo de ítems del lote cuya primera palabra no está en la tabla de términos
/// (M-26). Llega sin propuesta (M-45): la persona elige una categoría existente, crea una categoría nueva, lo marca
/// como ambiguo o lo deja sin decidir.
/// </summary>
public partial class BloqueTerminoNuevoViewModel : ObservableObject
{
    public BloqueTerminoNuevoDto Bloque { get; }

    public string Termino => Bloque.Termino;

    public int Cantidad => Bloque.IdsCatalogo.Count;

    public string CantidadTexto => Cantidad == 1 ? "1 producto" : $"{Cantidad} productos";

    public string Ejemplos => string.Join(" · ", Bloque.Ejemplos);

    [ObservableProperty]
    private CategoriaDto? _categoriaSeleccionada;

    /// <summary>
    /// La persona eligió crear una categoría nueva en lugar de usar una existente (P6-4).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsaCategoriaExistente))]
    private bool _crearCategoriaNueva;

    [ObservableProperty]
    private string _nombreCategoriaNueva = string.Empty;

    [ObservableProperty]
    private RubroDto? _rubroSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pendiente))]
    private bool _resuelto;

    [ObservableProperty]
    private string? _resultado;

    public bool Pendiente => !Resuelto;

    public bool UsaCategoriaExistente => !CrearCategoriaNueva;

    public BloqueTerminoNuevoViewModel(BloqueTerminoNuevoDto bloque)
    {
        Bloque = bloque ?? throw new ArgumentNullException(nameof(bloque));
    }
}
