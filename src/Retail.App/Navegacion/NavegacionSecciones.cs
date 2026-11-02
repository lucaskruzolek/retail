using Retail.App.Services;

namespace Retail.App.Navegacion;

/// <summary>
/// Navegación por secciones del shell: abre páginas por identificador, por atajo o por sección, siempre con la política
/// de acceso del rol del operador, y mantiene en <see cref="EstadoNavegacion"/> cuál está abierta. Es el único camino
/// por el que el menú lateral, los selectores de sección y los atajos F1 a F11 cambian de página.
/// </summary>
public sealed class NavegacionSecciones : INavegacionSecciones
{
    private readonly ICurrentUserSession _sesion;
    private readonly INavigationService _navegacion;
    private PaginaNavegacion? _pendiente;

    public NavegacionSecciones(ICurrentUserSession sesion, INavigationService navegacion, EstadoNavegacion? estado = null)
    {
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _navegacion = navegacion ?? throw new ArgumentNullException(nameof(navegacion));
        Estado = estado ?? new EstadoNavegacion();

        Estado.Cambio += () => PaginaActualCambio?.Invoke();
        _navegacion.Navigated += AlNavegar;
    }

    public EstadoNavegacion Estado { get; }

    public string? PaginaActualId => Estado.PaginaActualId;

    public string? SeccionActualId => Estado.SeccionActualId;

    public event Action? PaginaActualCambio;

    public IReadOnlyList<PaginaNavegacion> PaginasVisibles(string seccionId)
    {
        var seccion = RegistroNavegacion.BuscarSeccion(seccionId);

        return seccion is null ? [] : RegistroNavegacion.PaginasVisibles(seccion, _sesion.Rol);
    }

    /// <summary>Secciones del menú lateral que el operador actual puede abrir.</summary>
    public IReadOnlyList<SeccionNavegacion> SeccionesVisibles()
    {
        return RegistroNavegacion.SeccionesVisibles(_sesion.Rol);
    }

    public void IrA(string paginaId)
    {
        var ubicacion = RegistroNavegacion.Buscar(paginaId);

        if (ubicacion is not null)
        {
            Abrir(ubicacion.Value.Pagina);
        }
    }

    /// <summary>Abre la página que usa un atajo ("F4"). Devuelve si el atajo correspondía a una página permitida.</summary>
    public bool IrAAtajo(string atajo)
    {
        var pagina = RegistroNavegacion.BuscarPorAtajo(atajo);

        if (pagina is null || !PoliticaAcceso.Permite(pagina.Acceso, _sesion.Rol))
        {
            return false;
        }

        Abrir(pagina);

        return true;
    }

    /// <summary>Abre una sección por su acceso del menú: la última página que se visitó en ella, o la primera.</summary>
    public void IrASeccion(string seccionId)
    {
        var seccion = RegistroNavegacion.BuscarSeccion(seccionId);
        var pagina = seccion is null ? null : Estado.PaginaDeEntrada(seccion, _sesion.Rol);

        if (pagina is not null)
        {
            Abrir(pagina);
        }
    }

    /// <summary>Abre la página de aterrizaje del rol (el tablero del Gerente, el POS, o usuarios para el Administrador).</summary>
    public void IrAInicio()
    {
        var pagina = RegistroNavegacion.PaginaInicial(_sesion.Rol);

        if (pagina is not null)
        {
            Abrir(pagina);
        }
    }

    /// <summary>Olvida la ubicación: se usa al cambiar de operador.</summary>
    public void Reiniciar()
    {
        Estado.Reiniciar();
    }

    private void Abrir(PaginaNavegacion pagina)
    {
        if (!PoliticaAcceso.Permite(pagina.Acceso, _sesion.Rol))
        {
            return;
        }

        _pendiente = pagina;
        try
        {
            _navegacion.NavigateTo(pagina.TipoVista, pagina.Configurar);
        }
        finally
        {
            _pendiente = null;
        }

        // El servicio de navegación avisa con Navigated; si no lo hizo (un doble en pruebas), se registra igual
        if (Estado.PaginaActualId != pagina.Id)
        {
            Estado.Registrar(pagina.Id);
        }
    }

    // Navegación hecha por esta clase (la página pendiente) o por otra vía, como un enlace de otra pantalla
    private void AlNavegar(Type tipoVista)
    {
        if (_pendiente is { } pendiente && pendiente.TipoVista == tipoVista)
        {
            Estado.Registrar(pendiente.Id);
            return;
        }

        var actual = Estado.PaginaActualId is { } id ? RegistroNavegacion.Buscar(id) : null;
        if (actual?.Pagina.TipoVista == tipoVista)
        {
            return;
        }

        var pagina = RegistroNavegacion.BuscarPorVista(tipoVista);

        if (pagina is not null)
        {
            Estado.Registrar(pagina.Id);
        }
        else
        {
            Estado.MarcarFueraDelRegistro();
        }
    }
}
