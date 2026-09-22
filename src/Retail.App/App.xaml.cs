using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Retail.App.Views.Dialogs;
using Retail.Application;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Infrastructure;
using Retail.Infrastructure.Persistence.Context;
using Retail.Infrastructure.Persistence.Initialization;
using Serilog;

namespace Retail.App;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    /// <summary>
    /// Acceso al proveedor de servicios de inyección de dependencias para resolución desacoplada en arneses de desarrollo.
    /// </summary>
    public static IServiceProvider Services => ((App)Current)._host.Services;

    public App()
    {
        // 1. Inicialización temprana de Serilog (Bootstrap Logger)
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        var logFilePath = Path.Combine(logDirectory, "retail-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Iniciando Retail POS");

        // 2. Conectar manejadores globales de excepciones no controladas (Etapa 0.8)
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // 3. Construcción del Generic Host integrado con Serilog
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Inyección de capas del Clean Monolith
                services.AddApplicationServices();
                services.AddInfrastructureServices(context.Configuration);

                // Sesión de Usuario y Navegación Desacoplada (Módulo 1.1)
                services.AddSingleton<Retail.App.Services.ICurrentUserSession, Retail.App.Services.CurrentUserSession>();
                services.AddSingleton<Retail.App.Services.INavigationService, Retail.App.Services.NavigationService>();
                services.AddTransient<Retail.App.ViewModels.Auth.LoginViewModel>();
                services.AddTransient<Retail.App.Views.Auth.LoginWindow>();
                services.AddSingleton<Retail.App.ViewModels.MainViewModel>();
                services.AddSingleton<MainWindow>();

                // Vistas de Desarrollo
                services.AddTransient<Retail.App.Views.Dev.StyleGalleryView>();

                // Operadores y Usuarios (Módulo 1.2)
                services.AddSingleton<Retail.App.Services.IUsuarioDialogService, Retail.App.Services.UsuarioDialogService>();
                services.AddTransient<Retail.App.ViewModels.Usuarios.UsuariosViewModel>();
                services.AddTransient<Retail.App.Views.Pages.UsuariosView>();

                // Artículos y Catálogo (Módulo 2.1)
                services.AddSingleton<Retail.App.Services.IArticuloDialogService, Retail.App.Services.ArticuloDialogService>();
                services.AddTransient<Retail.App.ViewModels.Articulos.ArticulosViewModel>();
                services.AddTransient<Retail.App.ViewModels.Articulos.SeleccionarCatalogoProveedorModalViewModel>();
                services.AddTransient<Retail.App.Views.Pages.ArticulosView>();
                services.AddTransient<Retail.App.Views.Dialogs.SeleccionarCatalogoProveedorModalDialog>();

                services.AddSingleton<Retail.App.Services.IClienteDialogService, Retail.App.Services.ClienteDialogService>();
                services.AddTransient<Retail.App.ViewModels.Clientes.ClientesViewModel>();
                services.AddTransient<Retail.App.ViewModels.Clientes.ClienteFormViewModel>();
                services.AddTransient<Retail.App.Views.Pages.ClientesView>();

                // Proveedores y Catálogos de Proveedores (Módulo 2.2)
                services.AddSingleton<Retail.App.Services.IProveedorDialogService, Retail.App.Services.ProveedorDialogService>();
                services.AddTransient<Retail.App.ViewModels.Proveedores.ProveedoresViewModel>();
                services.AddTransient<Retail.App.ViewModels.Proveedores.ImportadorCatalogosViewModel>();
                services.AddTransient<Retail.App.Views.Pages.ProveedoresView>();
                services.AddTransient<Retail.App.Views.Pages.ImportadorCatalogosView>();
                services.AddTransient<Retail.App.Views.Dialogs.IncorporarArticulosModalDialog>();
                services.AddTransient<Retail.App.ViewModels.Proveedores.IncorporarArticulosModalViewModel>();
                services.AddTransient<Retail.App.Views.Dialogs.ImportarPlanillaDialog>();
                services.AddTransient<Retail.App.ViewModels.Proveedores.ImportarPlanillaViewModel>();
                services.AddTransient<Retail.App.Views.Dialogs.ProveedorFormDialog>();
                services.AddTransient<Retail.App.ViewModels.Proveedores.ProveedorFormViewModel>();
                services.AddTransient<Retail.App.Views.Dialogs.VincularArticuloModalDialog>();
                services.AddTransient<Retail.App.ViewModels.Proveedores.VincularArticuloModalViewModel>();
                // Punto de Venta y Mostrador (Módulo 4.1)
                services.AddSingleton<Retail.App.Services.IVentaDialogService, Retail.App.Services.VentaDialogService>();
                services.AddTransient<Retail.App.ViewModels.Ventas.PosViewModel>();
                services.AddTransient<Retail.App.Views.Pages.PosView>();

                // Módulos en Construcción (Cortesía informativa de Roadmap)

                services.AddTransient<Retail.App.Views.Pages.ModuloEnConstruccionView>();
            })

            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Control explícito de apagado durante la secuencia de autenticación
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Aplicar paleta Carmín / Borravino (#9D0F33) a todos los controles nativos de WPF-UI
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x9D, 0x0F, 0x33),
            Wpf.Ui.Appearance.ApplicationTheme.Light);

        await _host.StartAsync();

        // Inicialización y semillero de Base de Datos (Etapa 0.7)
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RetailDbContext>();
            await dbContext.Database.MigrateAsync();

            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            await DbInitializer.InitializeAsync(dbContext, passwordHasher);
        }

        // Flujo de autenticación obligatorio de inicio (RF-01)
        var session = _host.Services.GetRequiredService<Retail.App.Services.ICurrentUserSession>();
        var loginWindow = _host.Services.GetRequiredService<Retail.App.Views.Auth.LoginWindow>();

        var loginExitoso = loginWindow.ShowDialog();
        if (loginExitoso != true || !session.EstaAutenticado)
        {
            Log.Information("Inicio de sesión cancelado o ventana cerrada. Finalizando ejecución.");
            Shutdown();
            return;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            using (_host)
            {
                await _host.StopAsync();
            }
        }
        finally
        {
            Log.Information("Cierre ordenado de Retail POS. Purgando buffers de registro.");
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Excepción no controlada capturada en el Dispatcher de UI");

        ShowExceptionDialog(e.Exception, isFatal: false);

        // Marcar como manejada para evitar que WPF aborte el proceso en mostrador
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Excepción no controlada capturada en AppDomain. IsTerminating: {IsTerminating}", e.IsTerminating);

            if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                Dispatcher.Invoke(() => ShowExceptionDialog(ex, isFatal: e.IsTerminating));
            }
        }
        else
        {
            Log.Fatal("Excepción no controlada capturada en AppDomain: {ExceptionObject}. IsTerminating: {IsTerminating}", e.ExceptionObject, e.IsTerminating);
        }

        // Forzar vaciado de buffers antes de que el runtime de .NET pueda abortar el proceso
        Log.CloseAndFlush();
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Excepción asíncrona no observada capturada en TaskScheduler");

        // Marcar como observada para evitar escalamiento a crash de proceso en el CLR
        e.SetObserved();
    }

    private bool _isShowingExceptionDialog;

    private void ShowExceptionDialog(Exception ex, bool isFatal)
    {
        if (_isShowingExceptionDialog)
        {
            // Evitar reentrancia o cascada recursiva si la infraestructura de ventanas está en fallo
            return;
        }

        try
        {
            _isShowingExceptionDialog = true;

            var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", $"retail-{DateTime.Now:yyyyMMdd}.log");
            var dialog = new UnhandledExceptionDialog(ex, isFatal, logFile);

            if (MainWindow is { IsVisible: true })
            {
                dialog.Owner = MainWindow;
            }

            dialog.ShowDialog();
        }
        catch (Exception dialogEx)
        {
            Log.Fatal(dialogEx, "Error secundario al intentar desplegar UnhandledExceptionDialog");

            try
            {
                System.Windows.MessageBox.Show(
                    $"Ocurrió un error inesperado:\n\n{ex.Message}\n\nConsulte el archivo de registro en logs/retail-.log",
                    "Error en Retail POS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // Silenciar fallo de MessageBox para no propagar excepciones anidadas en el Dispatcher
            }
        }
        finally
        {
            _isShowingExceptionDialog = false;
        }
    }
}
