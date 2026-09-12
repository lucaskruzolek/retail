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

        // Salvaguarda: limitar dimensiones al área de trabajo útil de la pantalla
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

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
        if (e.Key == Key.F2)
        {
            ViewModel.NavegarArticulos();
            e.Handled = true;
        }
        else if (e.Key == Key.F10 && ViewModel.EsGerente)
        {
            ViewModel.NavegarUsuarios();
            e.Handled = true;
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
