using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.App.Services;
using Retail.App.Views.Dev;
using Retail.App.Views.Pages;

namespace Retail.App.ViewModels;

/// <summary>
/// ViewModel que gobierna el Shell principal de mostrador (RF-01, RF-02, RNF-06).
/// Expone información del operador activo, adapta los accesos según roles RBAC
/// y coordina la navegación de páginas desacoplada.
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
    private string _tituloModuloActual = "Catálogo de Artículos";

    /// <summary>
    /// Evento invocado cuando el usuario solicita cambiar de operador desde la barra superior.
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
    }

    private void OnNavigated(Type viewType)
    {
        if (viewType == typeof(ArticulosView))
        {
            TituloModuloActual = "Catálogo de Artículos y Control de Stock";
        }
        else if (viewType == typeof(UsuariosView))
        {
            TituloModuloActual = "Administración de Operadores y Roles RBAC";
        }
        else if (viewType == typeof(StyleGalleryView))
        {
            TituloModuloActual = "Galería de Estilos y Controles (Dev)";
        }
        else
        {
            TituloModuloActual = "Retail POS";
        }
    }

    [RelayCommand]
    public void NavegarArticulos()
    {
        _navigationService.NavigateTo<ArticulosView>();
    }

    [RelayCommand]
    public void NavegarUsuarios()
    {
        if (EsGerente)
        {
            _navigationService.NavigateTo<UsuariosView>();
        }
    }

    [RelayCommand]
    public void NavegarGaleriaDev()
    {
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
