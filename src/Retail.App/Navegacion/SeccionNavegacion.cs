using MahApps.Metro.IconPacks;

namespace Retail.App.Navegacion;

/// <summary>
/// Un acceso del menú lateral. Con varias páginas visibles para el rol, la página muestra bajo su título un selector
/// para pasar de una a otra; con una sola, el acceso abre directo la página y no hay selector.
/// </summary>
/// <param name="Id">Identificador estable de la sección.</param>
/// <param name="Titulo">Etiqueta del menú y título de las páginas de la sección.</param>
/// <param name="Icono">Ícono BoxIcons de variante Regular del menú.</param>
/// <param name="Paginas">Páginas en el orden en que se muestran.</param>
public sealed record SeccionNavegacion(
    string Id,
    string Titulo,
    PackIconBoxIconsKind Icono,
    IReadOnlyList<PaginaNavegacion> Paginas);
