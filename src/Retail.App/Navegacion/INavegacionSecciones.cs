namespace Retail.App.Navegacion;

/// <summary>
/// Lo que necesita el selector de sección de una página: qué páginas ofrece al operador actual, cuál está abierta y
/// cómo ir a otra. Lo implementa el shell; las pruebas del componente usan un doble.
/// </summary>
public interface INavegacionSecciones
{
    /// <summary>Identificador de la página abierta, o <c>null</c> si todavía no se navegó a ninguna.</summary>
    string? PaginaActualId { get; }

    /// <summary>Se produce cuando cambia <see cref="PaginaActualId"/>.</summary>
    event Action? PaginaActualCambio;

    /// <summary>Páginas de la sección que el rol del operador puede abrir, en orden.</summary>
    IReadOnlyList<PaginaNavegacion> PaginasVisibles(string seccionId);

    /// <summary>Abre la página, si el operador tiene acceso.</summary>
    void IrA(string paginaId);
}
