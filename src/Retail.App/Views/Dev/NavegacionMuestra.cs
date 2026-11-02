using Retail.App.Navegacion;

namespace Retail.App.Views.Dev;

/// <summary>
/// Navegación de muestra para la galería de estilos: ofrece todas las páginas de la sección sin filtrar por rol, recuerda
/// la página "abierta" en memoria y avisa del cambio, sin navegar de verdad. Alcanza para probar el
/// <c>NavegadorSeccion</c> sin el shell.
/// </summary>
public sealed class NavegacionMuestra : INavegacionSecciones
{
    private string? _paginaActualId;

    public NavegacionMuestra(string paginaInicialId)
    {
        _paginaActualId = paginaInicialId;
    }

    public string? PaginaActualId => _paginaActualId;

    public event Action? PaginaActualCambio;

    public IReadOnlyList<PaginaNavegacion> PaginasVisibles(string seccionId)
    {
        return RegistroNavegacion.BuscarSeccion(seccionId)?.Paginas ?? [];
    }

    public void IrA(string paginaId)
    {
        _paginaActualId = paginaId;
        PaginaActualCambio?.Invoke();
    }
}
