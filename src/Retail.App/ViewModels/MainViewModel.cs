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
    private string _tituloModuloActual = "Catálogo de Artículos y Control de Stock";

    [ObservableProperty]
    private bool _isSidebarExpanded;

    [ObservableProperty]
    private string _moduloActivo = "Articulos";

    public bool MostrarCabeceraAdmin => IsSidebarExpanded && EsGerente;

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

        _session.SessionChanged += ActualizarDatosOperador;
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
        OnPropertyChanged(nameof(MostrarCabeceraAdmin));
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
            TituloModuloActual = "Padrón de Proveedores e Importador de Catálogos";
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
    }

    [RelayCommand]
    public void NavegarPos()
    {
        ModuloActivo = "Pos";
        TituloModuloActual = "Punto de Venta (POS) y Cobro Multimedio";
        _navigationService.NavigateTo<PosView>();
    }

    [RelayCommand]
    public void NavegarArticulos()
    {
        ModuloActivo = "Articulos";
        _navigationService.NavigateTo<ArticulosView>();
    }

    [RelayCommand]
    public void NavegarClientes()
    {
        ModuloActivo = "Clientes";
        _navigationService.NavigateTo<ClientesView>();
    }

    [RelayCommand]
    public void NavegarCaja()
    {
        ModuloActivo = "Caja";
        TituloModuloActual = "Turnos de Caja y Arqueo Ciego";
        _navigationService.NavigateTo<ModuloEnConstruccionView>(view =>
        {
            view.Configurar(
                titulo: "Turnos de Caja y Arqueo Ciego",
                etapa: "Etapa 3: Épica 3 - Módulo 3.1",
                responsable: "Pablo Fernandez",
                requisitos: "RF-13, RF-14, RF-15",
                descripcion: "Apertura de turno con fondo inicial, movimientos extraordinarios de efectivo y arqueo ciego con acta comparativa.");
        });
    }

    [RelayCommand]
    public void NavegarPresupuestos()
    {
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
        ModuloActivo = "Compras";
        TituloModuloActual = "Compras a Proveedores y Recálculo de Precios";
        _navigationService.NavigateTo<ModuloEnConstruccionView>(view =>
        {
            view.Configurar(
                titulo: "Gestión de Compras y Recálculo por Markup",
                etapa: "Etapa 4: Épica 4 - Módulo 4.2",
                responsable: "Pablo Fernandez",
                requisitos: "RF-19",
                descripcion: "Ingreso de facturas de distribuidores con incremento de stock físico y recálculo automático e inmediato de precios de venta.");
        });
    }

    [RelayCommand]
    public void NavegarProveedores()
    {
        ModuloActivo = "Proveedores";
        TituloModuloActual = "Padrón de Proveedores e Importador de Catálogos";
        _navigationService.NavigateTo<ProveedoresView>();
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
        if (EsGerente)
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
