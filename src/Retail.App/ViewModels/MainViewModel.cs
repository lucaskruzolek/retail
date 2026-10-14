using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Services;
using Retail.App.Views.Dev;
using Retail.App.Views.Pages;

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
    /// Evento invocado cuando el usuario solicita cambiar de operador desde la barra o sidebar.
    /// Permite a MainWindow desplegar la ventana de login sin reiniciar el ejecutable (RF-02).
    /// </summary>
    public event Func<Task>? SolicitarCambioUsuario;

    public MainViewModel(
        ICurrentUserSession session,
        INavigationService navigationService)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

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
