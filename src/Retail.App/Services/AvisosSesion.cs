using System.Windows;

namespace Retail.App.Services;

/// <summary>
/// Implementación WPF de <see cref="IAvisosSesion"/>. Los avisos que llegan desde el hilo del latido se pasan siempre
/// al Dispatcher de la UI (Ley 4); los que ocurren con la ventana principal visible la usan de dueña.
/// </summary>
public sealed class AvisosSesion : IAvisosSesion
{
    private const string Titulo = "Retail POS - Sesión";

    public void RechazarInicio(string mensaje)
    {
        MessageBox.Show(mensaje, Titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void Informar(string mensaje)
    {
        MessageBox.Show(mensaje, Titulo, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public Task AvisarSesionPerdidaYSalirAsync()
    {
        return Dispatcher().InvokeAsync(() =>
        {
            Mostrar(
                "Su sesión se cerró porque se inició en otra terminal o un Gerente la cerró. La aplicación se va a cerrar.",
                MessageBoxImage.Warning);
            System.Windows.Application.Current.Shutdown();
        }).Task;
    }

    public void AvisarMantenimiento(string mensaje)
    {
        // Sin await: el hilo del latido no queda bloqueado esperando que alguien cierre el cuadro.
        _ = Dispatcher().InvokeAsync(() => Mostrar(mensaje, MessageBoxImage.Information));
    }

    public void AvisarAdvertencia(string mensaje)
    {
        _ = Dispatcher().InvokeAsync(() => Mostrar(mensaje, MessageBoxImage.Warning));
    }

    private static System.Windows.Threading.Dispatcher Dispatcher()
    {
        return System.Windows.Application.Current.Dispatcher;
    }

    private static void Mostrar(string mensaje, MessageBoxImage icono)
    {
        if (System.Windows.Application.Current.MainWindow is { IsVisible: true } ventana)
        {
            MessageBox.Show(ventana, mensaje, Titulo, MessageBoxButton.OK, icono);
            return;
        }

        MessageBox.Show(mensaje, Titulo, MessageBoxButton.OK, icono);
    }
}
