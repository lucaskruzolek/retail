using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.Services;
using Retail.App.ViewModels;
using Retail.App.Views.Auth;
using Wpf.Ui.Controls;

namespace Retail.App;

/// <summary>
/// Shell principal de mostrador y contenedor de navegación de Retail POS (RF-01, RF-02).
/// Inyectado estrictamente como Singleton conforme a la Ley 10 de Arquitectura.
/// Incorpora Sidebar colapsable para operadores y acceso con teclas rápidas a módulos.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;

    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        DataContext = ViewModel;

        _navigationService.Initialize(RootFrame);
        ViewModel.SolicitarCambioUsuario += RealizarCambioUsuarioAsync;

        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Página inicial según el rol: tablero para el Gerente, punto de venta para quien opera y usuarios para el Administrador.
        ViewModel.NavegarModuloInicial();

        // El operador ya inició sesión antes de crear el Shell: el aviso de restauración reciente se consulta acá (R-20).
        await ViewModel.CargarAvisoRestauracionAsync();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Atajo Ctrl+B para alternar el colapso del sidebar
        if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ViewModel.ToggleSidebar();
            e.Handled = true;
            return;
        }

        // Atajos de conmutación global con teclas de función: cada página declara el suyo en el registro de navegación
        // y la política de acceso decide si el rol puede abrirla; sin página asignada o sin permiso, la tecla sigue de largo
        if (e.Key is >= Key.F1 and <= Key.F12 && ViewModel.AbrirPorAtajo(e.Key.ToString()))
        {
            e.Handled = true;
        }
    }

    private Task RealizarCambioUsuarioAsync()
    {
        // El login tiene su propio scope (y su propio DbContext), que se descarta al cerrarse la ventana (H-19).
        using var scopeLogin = _serviceProvider.CreateScope();
        var loginWindow = scopeLogin.ServiceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Owner = this;

        loginWindow.ShowDialog();

        return Task.CompletedTask;
    }
}
