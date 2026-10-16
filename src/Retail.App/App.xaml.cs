using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.Views.Dialogs;
using Retail.Application;
using Retail.Application.DTOs.Sistema;
using Retail.Application.Interfaces.Services;
using Retail.Infrastructure;
using Retail.Infrastructure.Logging;
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
        // Los registros van a %LOCALAPPDATA%\Retail POS\logs (nunca junto al ejecutable en Program Files).
        var logFilePath = Path.Combine(UbicacionDeLogs.Directorio(), "retail-.log");

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
            // Validación de ciclos de vida: si un servicio Scoped (por ejemplo, el DbContext) se resuelve
            // desde el proveedor raíz, la app falla al arrancar en lugar de compartir el contexto en silencio (H-19).
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices((context, services) => RegistrarServicios(services, context.Configuration))
            .Build();
    }

    /// <summary>
    /// Registra todas las capas y los componentes de UI. Es público y estático para que las pruebas validen
    /// la configuración real de inyección de dependencias sin instanciar la aplicación WPF.
    /// </summary>
    /// <remarks>
    /// Ciclos de vida (H-19): el Shell (MainWindow, MainViewModel, navegación y sesión) es Singleton (Ley 10).
    /// Cada pantalla se resuelve en su propio scope (<see cref="NavigationService"/>), y los servicios de
    /// diálogo son Scoped para que un modal use el mismo DbContext que la pantalla que lo abre.
    /// </remarks>
    public static void RegistrarServicios(IServiceCollection services, IConfiguration configuration)
    {
        // Inyección de capas del Clean Monolith
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

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
        services.AddScoped<Retail.App.Services.IUsuarioDialogService, Retail.App.Services.UsuarioDialogService>();
        services.AddTransient<Retail.App.ViewModels.Usuarios.UsuariosViewModel>();
        services.AddTransient<Retail.App.Views.Pages.UsuariosView>();

        // Respaldos y Restauración (Track 6.3)
        services.AddScoped<Retail.App.Services.IBackupDialogService, Retail.App.Services.BackupDialogService>();
        services.AddSingleton<Retail.App.Services.IReinicioAplicacion, Retail.App.Services.ReinicioAplicacion>();
        services.AddTransient<Retail.App.ViewModels.Backups.BackupViewModel>();
        services.AddTransient<Retail.App.Views.Pages.BackupView>();

        // Artículos y Catálogo (Módulo 2.1)
        services.AddScoped<Retail.App.Services.IArticuloDialogService, Retail.App.Services.ArticuloDialogService>();
        services.AddTransient<Retail.App.ViewModels.Articulos.ArticulosViewModel>();
        services.AddTransient<Retail.App.ViewModels.Articulos.SeleccionarCatalogoProveedorModalViewModel>();
        services.AddTransient<Retail.App.Views.Pages.ArticulosView>();
        services.AddTransient<Retail.App.Views.Dialogs.SeleccionarCatalogoProveedorModalDialog>();

        services.AddScoped<Retail.App.Services.IClienteDialogService, Retail.App.Services.ClienteDialogService>();
        services.AddTransient<Retail.App.ViewModels.Clientes.ClientesViewModel>();
        services.AddTransient<Retail.App.ViewModels.Clientes.ClienteFormViewModel>();
        services.AddTransient<Retail.App.Views.Pages.ClientesView>();

        // Proveedores y Catálogos de Proveedores (Módulo 2.2)
        services.AddScoped<Retail.App.Services.IProveedorDialogService, Retail.App.Services.ProveedorDialogService>();
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
        services.AddScoped<Retail.App.Services.IVentaDialogService, Retail.App.Services.VentaDialogService>();
        services.AddTransient<Retail.App.ViewModels.Ventas.PosViewModel>();
        services.AddTransient<Retail.App.Views.Pages.PosView>();
        services.AddTransient<Retail.App.Views.Pages.CajaView>();
        services.AddTransient<Retail.App.ViewModels.Caja.CajaViewModel>();
        // Registro de la Vista de Compras y su ViewModel(Módulo 4.2)
        services.AddTransient<Retail.App.Views.Pages.ComprasView>();
        services.AddTransient<Retail.App.ViewModels.Compras.ComprasViewModel>();
        services.AddTransient<ICajaDialogService, CajaDialogService>();

        // Módulos en Construcción (Cortesía informativa de Roadmap)

        services.AddTransient<Retail.App.Views.Pages.ModuloEnConstruccionView>();
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

        // Verificación de la base (RF-23): la aplicación ya no migra ni siembra datos al arrancar. Si el esquema
        // no está al día o no hay un Gerente, informa qué comando de Retail.Cli ejecutar y no abre el login.
        EstadoSistemaDto estadoSistema;
        using (var scope = _host.Services.CreateScope())
        {
            estadoSistema = await scope.ServiceProvider.GetRequiredService<IEstadoSistemaService>().ObtenerEstadoAsync();
        }

        var diagnostico = MensajeEstadoSistema.Describir(estadoSistema);
        if (diagnostico.Bloquea)
        {
            Log.Warning("Arranque bloqueado por el estado de la base: {Mensaje}", diagnostico.Mensaje);
            new Retail.App.Views.Dialogs.DiagnosticoArranqueDialog(diagnostico).ShowDialog();
            Shutdown();
            return;
        }

        if (diagnostico.Mensaje is not null)
        {
            MessageBox.Show(diagnostico.Mensaje, "Retail POS - Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Flujo de autenticación obligatorio de inicio (RF-01)
        var session = _host.Services.GetRequiredService<Retail.App.Services.ICurrentUserSession>();

        // El login tiene su propio scope (y su propio DbContext), que se descarta al cerrarse la ventana (H-19).
        bool? loginExitoso;
        using (var scopeLogin = _host.Services.CreateScope())
        {
            var loginWindow = scopeLogin.ServiceProvider.GetRequiredService<Retail.App.Views.Auth.LoginWindow>();
            loginExitoso = loginWindow.ShowDialog();
        }

        if (loginExitoso != true || !session.EstaAutenticado)
        {
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

            var logFile = Path.Combine(UbicacionDeLogs.Directorio(), $"retail-{DateTime.Now:yyyyMMdd}.log");
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
                    $"Ocurrió un error inesperado:\n\n{ex.Message}\n\nConsulte el archivo de registro en {UbicacionDeLogs.Directorio()}",
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
