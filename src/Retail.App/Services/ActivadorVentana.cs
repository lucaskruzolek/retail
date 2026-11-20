using System.Windows;

namespace Retail.App.Services;

/// <summary>
/// Trae al frente la ventana de la instancia que ya está abierta cuando el usuario intenta abrir la aplicación otra vez
/// (MT-2). Se invoca desde el hilo que escucha la señal, así que pasa siempre por el Dispatcher de la UI.
/// </summary>
public static class ActivadorVentana
{
    public static void TraerAlFrente()
    {
        var aplicacion = System.Windows.Application.Current;
        if (aplicacion is null)
        {
            return;
        }

        _ = aplicacion.Dispatcher.InvokeAsync(() =>
        {
            // Mientras el login está abierto todavía no hay ventana principal: se activa la ventana visible.
            var ventana = aplicacion.MainWindow is { IsVisible: true } principal
                ? principal
                : aplicacion.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible);

            if (ventana is null)
            {
                return;
            }

            if (ventana.WindowState == WindowState.Minimized)
            {
                ventana.WindowState = WindowState.Normal;
            }

            ventana.Activate();
        });
    }
}
