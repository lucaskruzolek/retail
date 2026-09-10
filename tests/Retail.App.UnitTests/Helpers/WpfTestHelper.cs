using System.Windows;
using System.Windows.Threading;

namespace Retail.App.UnitTests.Helpers;

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
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Retail.App;component/Styles/Controls.xaml", UriKind.Absolute) });
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
