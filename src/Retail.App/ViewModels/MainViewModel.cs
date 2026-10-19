using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Converters;
using Retail.App.Services;
using Retail.App.Views.Dev;
using Retail.App.Views.Pages;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MainViewModel> _logger;

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
    private string _tituloModuloActual = "Catálogo de Artículos y Control de Stock";

    [ObservableProperty]
    private bool _isSidebarExpanded;

    [ObservableProperty]
    private string _moduloActivo = "Articulos";

    /// <summary>
    /// Gerente y Administrador gestionan cuentas (RF-03). Qué cuentas puede tocar cada uno lo decide
    /// <c>UsuarioService</c>; acá solo se decide si el módulo se muestra.
    /// </summary>
    public bool PuedeGestionarUsuarios => EsGerente || EsAdministrador;

    /// <summary>
    /// El Administrador es soporte técnico: no opera el POS, la caja ni el inventario (RF-03, D-3).
    /// </summary>
    public bool PuedeOperarNegocio => !EsAdministrador;

    public bool MostrarCabeceraAdmin => IsSidebarExpanded && PuedeGestionarUsuarios;

    public bool MostrarCabecerasOperativas => IsSidebarExpanded && PuedeOperarNegocio;

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
    /// </remarks>
    public MainViewModel(
        ICurrentUserSession session,
        INavigationService navigationService,
        IServiceScopeFactory scopeFactory,
        ILogger<MainViewModel>? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? NullLogger<MainViewModel>.Instance;

        _session.SessionChanged += OnSessionChanged;
        _navigationService.Navigated += OnNavigated;

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
        OnPropertyChanged(nameof(MostrarCabeceraAdmin));
        OnPropertyChanged(nameof(MostrarCabecerasOperativas));
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
            NavegarModuloInicial();
        }

        _ = CargarAvisoRestauracionAsync();
    }

    /// <summary>
    /// Consulta la última restauración y, si ocurrió en los últimos días, arma el aviso para el Gerente o el
    /// Administrador. Nunca lanza: un fallo al consultar no debe impedir trabajar (el aviso es informativo).
    /// </summary>
    public async Task CargarAvisoRestauracionAsync()
    {
        AvisoRestauracion = null;

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

            AvisoRestauracion =
                $"Se restauró la base el {fechaUtc.ToLocalTime():dd/MM/yyyy HH:mm} por {ultima.NombreUsuario} " +
                $"({EnumATextoConverter.ATexto(ultima.Origen)}).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar la última restauración: {Mensaje}", ex.Message);
        }
    }

    [RelayCommand]
    public void CerrarAvisoRestauracion()
    {
        AvisoRestauracion = null;
    }

    /// <summary>
    /// Primera página del Shell según el rol: el catálogo para quien opera el negocio y la gestión de usuarios
    /// para el Administrador.
    /// </summary>
    public void NavegarModuloInicial()
    {
        if (PuedeOperarNegocio)
        {
            NavegarArticulos();
        }
        else
        {
            NavegarUsuarios();
        }
    }

    private void OnNavigated(Type viewType)
    {
        if (viewType == typeof(PosView))
        {
            ModuloActivo = "Pos";
            TituloModuloActual = "Punto de Venta (POS) y Cobro Multimedio";
        }
        else if (viewType == typeof(ArticulosView))
        {
            ModuloActivo = "Articulos";
            TituloModuloActual = "Catálogo de Artículos y Control de Stock";
        }
        else if (viewType == typeof(ClientesView))
        {
            ModuloActivo = "Clientes";
            TituloModuloActual = "Padrón de Clientes y Cuentas Corrientes";
        }
        else if (viewType == typeof(UsuariosView))
        {
            ModuloActivo = "Usuarios";
            TituloModuloActual = "Administración de Operadores y Roles RBAC";
        }
        else if (viewType == typeof(BackupView))
        {
            ModuloActivo = "Respaldos";
            TituloModuloActual = "Copias de Seguridad y Restauración";
        }
        else if (viewType == typeof(DashboardView))
        {
            ModuloActivo = "Tablero";
            TituloModuloActual = "Tablero gerencial";
        }
        else if (viewType == typeof(StyleGalleryView))
        {
            ModuloActivo = "Galeria";
            TituloModuloActual = "Galería de Estilos y Controles (Dev)";
        }
        else if (viewType == typeof(ProveedoresView))
        {
            ModuloActivo = "Proveedores";
            TituloModuloActual = "Padrón de Proveedores";
        }
        else if (viewType == typeof(ImportadorCatalogosView))
        {
            ModuloActivo = "Catalogos";
            TituloModuloActual = "Catálogos de Proveedores e Importación Masiva";
        }
        else if (viewType == typeof(ComprasView))
        {
            ModuloActivo = "Compras";
            TituloModuloActual = "Compras a Proveedores y Recálculo de Precios";
        }
        else if (viewType == typeof(ModuloEnConstruccionView))
        {
            // El título es establecido por el comando de navegación que invoca la vista
        }
        else
        {
            TituloModuloActual = "Retail POS";
        }
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        OnPropertyChanged(nameof(MostrarCabeceraAdmin));
        OnPropertyChanged(nameof(MostrarCabecerasOperativas));
    }

    [RelayCommand]
    public void NavegarPos()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Pos";
        TituloModuloActual = "Punto de Venta (POS) y Cobro Multimedio";
        _navigationService.NavigateTo<PosView>();
    }

    [RelayCommand]
    public void NavegarArticulos()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Articulos";
        _navigationService.NavigateTo<ArticulosView>();
    }

    [RelayCommand]
    public void NavegarClientes()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Clientes";
        _navigationService.NavigateTo<ClientesView>();
    }

    [RelayCommand]
    public void NavegarCaja()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Caja";
        TituloModuloActual = "Turnos de Caja y Arqueo Ciego";
        _navigationService.NavigateTo<CajaView>();
    }

    [RelayCommand]
    public void NavegarPresupuestos()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Presupuestos";
        TituloModuloActual = "Presupuestador Comercial Independiente";
        _navigationService.NavigateTo<ModuloEnConstruccionView>(view =>
        {
            view.Configurar(
                titulo: "Presupuestador Independiente y Conversión",
                etapa: "Etapa 5: Épica 5 - Módulo 5.1",
                responsable: "Pablo Fernandez",
                requisitos: "RF-11, RF-12",
                descripcion: "Emisión de cotizaciones independientes con congelamiento de precios por 15 días y conversión adaptativa a venta con control de stock.");
        });
    }

    [RelayCommand]
    public void NavegarCompras()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Compras";
        TituloModuloActual = "Compras a Proveedores y Recálculo de Precios";
        _navigationService.NavigateTo<ComprasView>();
    }

    [RelayCommand]
    public void NavegarProveedores()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Proveedores";
        TituloModuloActual = "Padrón de Proveedores";
        _navigationService.NavigateTo<ProveedoresView>();
    }

    [RelayCommand]
    public void NavegarCatalogos()
    {
        if (!PuedeOperarNegocio)
        {
            return;
        }

        ModuloActivo = "Catalogos";
        TituloModuloActual = "Catálogos de Proveedores e Importación Masiva";
        _navigationService.NavigateTo<ImportadorCatalogosView>();
    }

    [RelayCommand]
    public void NavegarTablero()
    {
        if (!EsGerente)
        {
            return;
        }

        ModuloActivo = "Tablero";
        _navigationService.NavigateTo<DashboardView>();
    }

    [RelayCommand]
    public void NavegarConsolaFiscal()
    {
        if (!EsGerente)
        {
            return;
        }

        ModuloActivo = "Fiscal";
        TituloModuloActual = "Consola Fiscal ARCA y Contingencia";
        _navigationService.NavigateTo<ModuloEnConstruccionView>(view =>
        {
            view.Configurar(
                titulo: "Consola Fiscal ARCA y Contingencia Resiliente",
                etapa: "Etapa 5: Épica 5 - Módulo 5.2",
                responsable: "Lucas Kruzolek",
                requisitos: "RF-16, RF-17, RF-18",
                descripcion: "Panel exclusivo gerencial para monitoreo de comprobantes en contingencia y reintento secuencial en lote ante ARCA.");
        });
    }

    [RelayCommand]
    public void NavegarUsuarios()
    {
        if (PuedeGestionarUsuarios)
        {
            ModuloActivo = "Usuarios";
            _navigationService.NavigateTo<UsuariosView>();
        }
    }

    [RelayCommand]
    public void NavegarRespaldos()
    {
        if (PuedeGestionarUsuarios)
        {
            ModuloActivo = "Respaldos";
            _navigationService.NavigateTo<BackupView>();
        }
    }

    [RelayCommand]
    public void NavegarGaleriaDev()
    {
        ModuloActivo = "Galeria";
        _navigationService.NavigateTo<StyleGalleryView>();
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
