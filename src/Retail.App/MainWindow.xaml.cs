using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.Services;
using Retail.App.ViewModels;
using Retail.App.Views.Auth;
using Retail.App.Views.Pages;
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

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Navegación predeterminada al módulo de catálogo de artículos
        _navigationService.NavigateTo<ArticulosView>();
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

        // Atajos de conmutación global de módulos con teclas de función (F1 a F10)
        switch (e.Key)
        {
            case Key.F1:
                ViewModel.NavegarPos();
                e.Handled = true;
                break;

            case Key.F2:
                ViewModel.NavegarArticulos();
                e.Handled = true;
                break;

            case Key.F4:
                ViewModel.NavegarClientes();
                e.Handled = true;
                break;

            case Key.F5:
                ViewModel.NavegarCaja();
                e.Handled = true;
                break;

            case Key.F6:
                ViewModel.NavegarPresupuestos();
                e.Handled = true;
                break;

            case Key.F9 when ViewModel.EsGerente:
                ViewModel.NavegarConsolaFiscal();
                e.Handled = true;
                break;

            case Key.F10 when ViewModel.EsGerente:
                ViewModel.NavegarUsuarios();
                e.Handled = true;
                break;
        }
    }

    private Task RealizarCambioUsuarioAsync()
    {
        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Owner = this;

        loginWindow.ShowDialog();

        return Task.CompletedTask;
    }
}
