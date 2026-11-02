namespace Retail.App.Navegacion;

/// <summary>
/// Una página del sistema dentro de una sección. <paramref name="Configurar"/> prepara la vista recién creada cuando
/// una misma vista sirve a varias páginas (Copias y Restauraciones) o es un marcador de módulo en construcción.
/// </summary>
/// <param name="Id">Identificador estable (minúsculas, sin espacios); es la clave de la navegación y de las pruebas.</param>
/// <param name="Titulo">Texto de la pestaña del selector de sección.</param>
/// <param name="TipoVista">Vista que se muestra en el <c>Frame</c>.</param>
/// <param name="Acceso">Quién puede abrirla.</param>
/// <param name="Atajo">Tecla global (por ejemplo "F4") que abre la página, o <c>null</c>.</param>
/// <param name="Configurar">Ajuste de la vista creada, o <c>null</c>.</param>
public sealed record PaginaNavegacion(
    string Id,
    string Titulo,
    Type TipoVista,
    AccesoPagina Acceso,
    string? Atajo = null,
    Action<object>? Configurar = null);
