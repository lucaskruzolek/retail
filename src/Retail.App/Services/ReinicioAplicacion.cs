using System.Diagnostics;

namespace Retail.App.Services;

/// <summary>
/// Reinicio real: lanza el mismo ejecutable y apaga la aplicación WPF actual.
/// </summary>
public class ReinicioAplicacion : IReinicioAplicacion
{
    public void Reiniciar()
    {
        var rutaEjecutable = Environment.ProcessPath
            ?? throw new InvalidOperationException("No se pudo determinar el ejecutable de la aplicación.");

        // La nueva instancia arranca antes de cerrar esta: si no pudiera iniciarse, la actual sigue abierta.
        Process.Start(new ProcessStartInfo(rutaEjecutable) { UseShellExecute = true });

        System.Windows.Application.Current.Shutdown();
    }
}
