using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.Services;
using Retail.App.ViewModels;
using Retail.App.Views.Auth;
using Wpf.Ui.Controls;

namespace Retail.App;

/// <summary>
/// Shell principal de mostrador y contenedor de navegaciÃ³n de Retail POS (RF-01, RF-02).
/// Inyectado estrictamente como Singleton conforme a la Ley 10 de Arquitectura.
/// Incorpora Sidebar colapsable para operadores y acceso con teclas rÃ¡pidas a mÃ³dulos.
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
        // PÃ¡gina inicial segÃºn el rol: catÃ¡logo de artÃ­culos o, para el Administrador, gestiÃ³n de usuarios.
        ViewModel.NavegarModuloInicial();

        // El operador ya iniciÃ³ sesiÃ³n antes de crear el Shell: el aviso de restauraciÃ³n reciente se consulta acÃ¡ (R-20).
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

        // Atajos de conmutaciÃ³n global de mÃ³dulos con teclas de funciÃ³n (F1 a F10)
        switch (e.Key)
        {
            case Key.F1:
                ViewModel.NavegarPos();
                e.Handled = true;
                break;

            case Key.F2:
                // F2 en Compras está reservado para `Agregar Artículo` en la pantalla local (Bug fix: track 4.3).
                // Si el módulo activo es Compras, no interceptamos aquí: el KeyBinding de ComprasView lo maneja.
                if (ViewModel.ModuloActivo != "Compras")
                {
                    ViewModel.NavegarArticulos();
                    e.Handled = true;
                }
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

            case Key.F10 when ViewModel.PuedeGestionarUsuarios:
                ViewModel.NavegarUsuarios();
                e.Handled = true;
                break;

            case Key.F11 when ViewModel.PuedeGestionarUsuarios:
                ViewModel.NavegarRespaldos();
                e.Handled = true;
                break;
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
