using System.Windows;
using System.Windows.Threading;

namespace Retail.App.UnitTests.Helpers;

/// <summary>
/// Tema visual con el que se renderizan las vistas en las pruebas.
/// </summary>
public enum TemaPrueba
{
    Claro,
    Oscuro
}

/// <summary>
/// Provee un despachador (Dispatcher) único en hilo STA persistente para la suite de pruebas unitarias de WPF,
/// evitando colisiones de afinidad de hilo (Thread Affinity) y excepciones entre pruebas xUnit.
/// </summary>
public static class WpfTestHelper
{
    private static readonly Lazy<Dispatcher> s_dispatcherLazy = new(() =>
    {
        var tcs = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            if (System.Windows.Application.Current == null)
            {
                try
                {
                    _ = new System.Windows.Application();
                }
                catch (InvalidOperationException)
                {
                    // Ignorar si ya fue instanciada en el AppDomain
                }
            }

            var app = System.Windows.Application.Current;
            if (app != null && app.Resources.MergedDictionaries.Count == 0)
            {
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Light });
                app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Colors.xaml", UriKind.Absolute) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Typography.xaml", UriKind.Absolute) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Icons.xaml", UriKind.Absolute) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Spacing.xaml", UriKind.Absolute) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Controls.xaml", UriKind.Absolute) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Listados.xaml", UriKind.Absolute) });

                // Igual que App.OnStartup: aplicar el acento antes de crear la primera ventana. Sin este paso, el
                // primer ui:TitleBar instanciado carga el acento azul de Windows en toda la aplicación.
                Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
                    System.Windows.Media.Color.FromRgb(0x9D, 0x0F, 0x33),
                    Wpf.Ui.Appearance.ApplicationTheme.Light);
            }

            tcs.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "Retail_WpfTest_STA_Thread";
        thread.Start();

        return tcs.Task.GetAwaiter().GetResult();
    });

    /// <summary>
    /// Cambia el tema de la <c>Application</c> compartida. Debe invocarse dentro de <see cref="Run(Action)"/> (hilo STA).
    /// Localiza los diccionarios por su posición en <c>MergedDictionaries</c>, tal como los arma este helper y
    /// <c>App.xaml</c>: [0] ThemesDictionary de WPF-UI y [2] paleta de Retail (Colors.xaml o Colors.Dark.xaml).
    /// Quien lo use debe restaurar el tema claro al terminar, porque la Application se comparte en toda la suite.
    /// </summary>
    public static void AplicarTema(TemaPrueba tema)
    {
        var app = System.Windows.Application.Current;
        var esOscuro = tema == TemaPrueba.Oscuro;
        var temaWpfUi = esOscuro ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
        var paleta = esOscuro ? "Colors.Dark.xaml" : "Colors.xaml";
        var acento = esOscuro
            ? System.Windows.Media.Color.FromRgb(0xD6, 0x3A, 0x62)
            : System.Windows.Media.Color.FromRgb(0x9D, 0x0F, 0x33);

        app.Resources.MergedDictionaries[0] = new Wpf.Ui.Markup.ThemesDictionary { Theme = temaWpfUi };
        app.Resources.MergedDictionaries[2] = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Retail.App;component/Styles/" + paleta, UriKind.Absolute)
        };

        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(acento, temaWpfUi);
    }

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        s_dispatcherLazy.Value.Invoke(action);
    }

    public static T Run<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return s_dispatcherLazy.Value.Invoke(func);
    }
}
