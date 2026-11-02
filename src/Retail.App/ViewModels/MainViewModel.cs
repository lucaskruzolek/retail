using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Converters;
using Retail.App.Navegacion;
using Retail.App.Services;
using Retail.App.Views.Dev;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels;

/// <summary>
/// ViewModel que gobierna el Shell principal de mostrador (RF-01, RF-02, RNF-06).
/// Expone información del operador activo, adapta los accesos según roles RBAC,
/// controla el colapso del sidebar y coordina la navegación de páginas desacoplada.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ICurrentUserSession _session;
    private readonly INavigationService _navigationService;
    private readonly NavegacionSecciones _navegacion;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfirmacionesAvisoRestauracion? _confirmaciones;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>
    /// Operador y restauración del aviso que se está mostrando. Se capturan al cargarlo para que un cambio de
    /// sesión entre la carga y el cierre no confirme la restauración a nombre de otro operador.
    /// </summary>
    private int? _idUsuarioAvisado;
    private Guid? _referenciaAvisada;

    /// <summary>
    /// Días durante los cuales una restauración se avisa al iniciar sesión (R-20).
    /// </summary>
    private const int DiasAvisoRestauracion = 7;

    [ObservableProperty]
    private string _operadorNombre = string.Empty;

    [ObservableProperty]
    private string _operadorRol = string.Empty;

    [ObservableProperty]
    private bool _esGerente;

    [ObservableProperty]
    private bool _esEncargado;

    [ObservableProperty]
    private bool _esCajero;

    [ObservableProperty]
    private bool _esAdministrador;

    [ObservableProperty]
    private bool _isSidebarExpanded;

    /// <summary>La galería de estilos (desarrollo) está abierta: no pertenece a ninguna sección del menú.</summary>
    [ObservableProperty]
    private bool _galeriaActiva;

    /// <summary>
    /// Gerente y Administrador gestionan cuentas (RF-03). Qué cuentas puede tocar cada uno lo decide
    /// <c>UsuarioService</c>; acá solo se decide si el módulo se muestra.
    /// </summary>
    public bool PuedeGestionarUsuarios => EsGerente || EsAdministrador;

    /// <summary>
    /// El Administrador es soporte técnico: no opera el POS, la caja ni el inventario (RF-03, D-3).
    /// </summary>
    public bool PuedeOperarNegocio => !EsAdministrador;

    /// <summary>
    /// Pedidos a proveedor: solo Encargado y Gerente; el Cajero no tiene acceso (A-14 del Track 4.3, G1).
    /// </summary>
    public bool PuedeAbastecer => EsGerente || EsEncargado;

    /// <summary>Accesos del menú lateral que el rol del operador puede abrir (la política sale de <see cref="RegistroNavegacion"/>).</summary>
    public ObservableCollection<ItemMenuViewModel> SeccionesMenu { get; } = [];

    /// <summary>Acceso fijo a Ajustes, en la zona inferior del menú.</summary>
    public ItemMenuViewModel ItemAjustes { get; } = new(RegistroNavegacion.Ajustes, RegistroNavegacion.Ajustes.Paginas);

    /// <summary>
    /// Aviso no bloqueante de una restauración reciente, para Gerente y Administrador (R-20): un restore
    /// equivocado o malicioso no debe pasar inadvertido.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarAvisoRestauracion))]
    private string? _avisoRestauracion;

    public bool MostrarAvisoRestauracion => !string.IsNullOrWhiteSpace(AvisoRestauracion);

    /// <summary>
    /// Evento invocado cuando el usuario solicita cambiar de operador desde la barra o sidebar.
    /// Permite a MainWindow desplegar la ventana de login sin reiniciar el ejecutable (RF-02).
    /// </summary>
    public event Func<Task>? SolicitarCambioUsuario;

    /// <remarks>
    /// El Shell es Singleton (Ley 10) y <see cref="IRestauracionService"/> es Scoped: para consultar la última
    /// restauración se abre un scope propio en lugar de inyectar el servicio. Sin fábrica de scopes no hay aviso.
    /// Sin <paramref name="confirmaciones"/> el aviso nunca se da por confirmado y reaparece en cada inicio de sesión.
    /// </remarks>
    public MainViewModel(
        ICurrentUserSession session,
        INavigationService navigationService,
        IServiceScopeFactory scopeFactory,
        IConfirmacionesAvisoRestauracion? confirmaciones = null,
        ILogger<MainViewModel>? logger = null,
        NavegacionSecciones? navegacion = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _confirmaciones = confirmaciones;
        _logger = logger ?? NullLogger<MainViewModel>.Instance;

        _navegacion = navegacion ?? new NavegacionSecciones(_session, _navigationService);

        _session.SessionChanged += OnSessionChanged;
        _navegacion.PaginaActualCambio += ActualizarSeccionActiva;

        ActualizarDatosOperador();
    }

    private void ActualizarDatosOperador()
    {
        OperadorNombre = _session.NombreCompleto;
        OperadorRol = _session.Rol?.ToString() ?? string.Empty;
        EsGerente = _session.EsGerente;
        EsEncargado = _session.EsEncargado;
        EsCajero = _session.EsCajero;
        EsAdministrador = _session.EsAdministrador;
        OnPropertyChanged(nameof(PuedeGestionarUsuarios));
        OnPropertyChanged(nameof(PuedeOperarNegocio));
        OnPropertyChanged(nameof(PuedeAbastecer));
        ReconstruirMenu();
    }

    partial void OnIsSidebarExpandedChanged(bool value)
    {
        foreach (var item in SeccionesMenu)
        {
            item.IsSidebarExpanded = value;
        }
    }

    private void ReconstruirMenu()
    {
        SeccionesMenu.Clear();

        foreach (var seccion in _navegacion.SeccionesVisibles())
        {
            SeccionesMenu.Add(new ItemMenuViewModel(seccion, _navegacion.PaginasVisibles(seccion.Id)) { IsSidebarExpanded = IsSidebarExpanded });
        }

        ActualizarSeccionActiva();
    }

    private void ActualizarSeccionActiva()
    {
        var activa = _navegacion.SeccionActualId;

        foreach (var item in SeccionesMenu)
        {
            item.EsActiva = item.Id == activa;
        }

        ItemAjustes.EsActiva = activa == RegistroNavegacion.Ajustes.Id;

        if (activa is not null)
        {
            GaleriaActiva = false;
        }
    }

    /// <summary>
    /// Al cambiar de operador (RF-02) se vuelve al módulo inicial del nuevo rol: la página abierta por el
    /// operador anterior puede no estar permitida para el nuevo (por ejemplo, el POS para un Administrador).
    /// </summary>
    private void OnSessionChanged()
    {
        ActualizarDatosOperador();

        if (_session.EstaAutenticado)
        {
            _navegacion.Reiniciar();
            NavegarModuloInicial();
        }

        _ = CargarAvisoRestauracionAsync();
    }

    /// <summary>
    /// Consulta la última restauración y, si ocurrió en los últimos días y el operador todavía no la confirmó,
    /// arma el aviso para el Gerente o el Administrador. Nunca lanza: un fallo al consultar no debe impedir
    /// trabajar (el aviso es informativo). Si no se puede leer la confirmación, el aviso se muestra igual: es una
    /// advertencia de seguridad y ante la duda se prefiere avisar de más.
    /// </summary>
    public async Task CargarAvisoRestauracionAsync()
    {
        AvisoRestauracion = null;
        _idUsuarioAvisado = null;
        _referenciaAvisada = null;

        if (!_session.EstaAutenticado || !PuedeGestionarUsuarios)
        {
            return;
        }

        var idActor = _session.IdUsuario ?? 0;

        try
        {
            var ultima = await Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var servicio = scope.ServiceProvider.GetRequiredService<IRestauracionService>();
                return await servicio.ObtenerUltimaRestauracionAsync(idActor);
            });

            if (ultima is null)
            {
                return;
            }

            var fechaUtc = DateTime.SpecifyKind(ultima.Fecha, DateTimeKind.Utc);
            if (DateTime.UtcNow - fechaUtc > TimeSpan.FromDays(DiasAvisoRestauracion))
            {
                return;
            }

            if (await EstaConfirmadaPorElOperadorAsync(idActor, ultima.Referencia))
            {
                return;
            }

            _idUsuarioAvisado = idActor;
            _referenciaAvisada = ultima.Referencia;
            AvisoRestauracion =
                $"Se restauró la base el {fechaUtc.ToLocalTime():dd/MM/yyyy HH:mm} por {ultima.NombreUsuario} " +
                $"({EnumATextoConverter.ATexto(ultima.Origen)}).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar la última restauración: {Mensaje}", ex.Message);
        }
    }

    private async Task<bool> EstaConfirmadaPorElOperadorAsync(int idUsuario, Guid referencia)
    {
        if (_confirmaciones is null)
        {
            return false;
        }

        try
        {
            return await Task.Run(() => _confirmaciones.EstaConfirmadaAsync(idUsuario, referencia));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer la confirmación del aviso de restauración: {Mensaje}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Oculta el aviso en el acto y registra, sin bloquear la interfaz, que el operador lo leyó para que no
    /// reaparezca en su próximo inicio de sesión. Si no se puede guardar la confirmación solo se registra el
    /// fallo: el aviso ya se cerró y el operador puede seguir trabajando.
    /// </summary>
    [RelayCommand]
    public async Task CerrarAvisoRestauracionAsync()
    {
        var idUsuario = _idUsuarioAvisado;
        var referencia = _referenciaAvisada;

        AvisoRestauracion = null;
        _idUsuarioAvisado = null;
        _referenciaAvisada = null;

        if (_confirmaciones is null || idUsuario is null || referencia is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => _confirmaciones.ConfirmarAsync(idUsuario.Value, referencia.Value));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo guardar la confirmación del aviso de restauración: {Mensaje}", ex.Message);
        }
    }

    /// <summary>
    /// Primera página del Shell según el rol: el tablero para el Gerente, el punto de venta para quien opera el negocio
    /// y la gestión de usuarios para el Administrador (la primera de la primera sección que el rol puede abrir).
    /// </summary>
    public void NavegarModuloInicial()
    {
        _navegacion.IrAInicio();
    }

    /// <summary>Abre la sección de un acceso del menú lateral (la última página que se visitó en ella, o la primera).</summary>
    [RelayCommand]
    public void IrASeccion(string seccionId)
    {
        _navegacion.IrASeccion(seccionId);
    }

    /// <summary>Abre una página por su identificador, si el rol puede.</summary>
    [RelayCommand]
    public void IrAPagina(string paginaId)
    {
        _navegacion.IrA(paginaId);
    }

    /// <summary>Atajo de teclado global (F1 a F11): abre la página asignada si el rol puede. Devuelve si la atendió.</summary>
    public bool AbrirPorAtajo(string atajo)
    {
        return _navegacion.IrAAtajo(atajo);
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    [RelayCommand]
    public void NavegarGaleriaDev()
    {
        _navigationService.NavigateTo<StyleGalleryView>();
        GaleriaActiva = true;
    }

    [RelayCommand]
    public async Task CambiarUsuarioAsync()
    {
        if (SolicitarCambioUsuario != null)
        {
            await SolicitarCambioUsuario.Invoke();
        }
    }
}
