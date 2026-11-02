using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;
using Retail.App.Navegacion;

namespace Retail.App.ViewModels;

/// <summary>
/// Un acceso del menú lateral: su ícono, su título y si es el de la sección abierta. El tooltip suma el atajo (si la
/// sección es una sola página) o la lista de páginas que contiene, para que el menú de pocos iconos siga siendo
/// descubrible.
/// </summary>
public sealed partial class ItemMenuViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _esActiva;

    /// <summary>
    /// Refleja si el menú está expandido. El estilo de los botones del menú (<c>SidebarItemButtonStyle</c>) lo lee del
    /// contexto de datos del botón, que acá es el ítem y no el shell: <see cref="MainViewModel"/> lo mantiene al día.
    /// </summary>
    [ObservableProperty]
    private bool _isSidebarExpanded;

    public ItemMenuViewModel(SeccionNavegacion seccion, IReadOnlyList<PaginaNavegacion> paginasVisibles)
    {
        ArgumentNullException.ThrowIfNull(seccion);
        ArgumentNullException.ThrowIfNull(paginasVisibles);

        Id = seccion.Id;
        Titulo = seccion.Titulo;
        Icono = seccion.Icono;

        if (paginasVisibles.Count == 1)
        {
            Atajo = paginasVisibles[0].Atajo;
        }
        else if (paginasVisibles.Count > 1)
        {
            Detalle = string.Join(" · ", paginasVisibles.Select(p => p.Titulo));
        }
    }

    public string Id { get; }

    public string Titulo { get; }

    public PackIconBoxIconsKind Icono { get; }

    /// <summary>Atajo de teclado de la página, si el acceso abre directo una sola.</summary>
    public string? Atajo { get; }

    /// <summary>Páginas de la sección separadas por "·", si tiene varias visibles.</summary>
    public string? Detalle { get; }

    public bool TieneAtajo => Atajo is not null;

    public bool TieneDetalle => Detalle is not null;
}
