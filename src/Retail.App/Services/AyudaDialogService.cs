using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.ViewModels.Ayuda;
using Retail.App.Views.Dialogs;

namespace Retail.App.Services;

/// <summary>
/// Implementación del servicio de presentación y soporte para el Centro de Ayuda y apertura del manual PDF.
/// </summary>
public class AyudaDialogService : IAyudaDialogService
{
    private const string NombreArchivoManual = "Manual_Usuario_Retail_POS.pdf";
    private readonly ILogger<AyudaDialogService> _logger;

    public AyudaDialogService(ILogger<AyudaDialogService>? logger = null)
    {
        _logger = logger ?? NullLogger<AyudaDialogService>.Instance;
    }

    public void MostrarCentroAyuda()
    {
        var viewModel = new AyudaViewModel(this);
        var dialog = new AyudaDialog(viewModel);

        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        dialog.ShowDialog();
    }

    public bool AbrirManualPdf()
    {
        var ruta = ObtenerRutaManual();

        if (!File.Exists(ruta))
        {
            _logger.LogWarning("No se encontró el archivo del manual de usuario en la ruta esperada: {Ruta}", ruta);
            return false;
        }

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Iniciando visor del sistema operativo para el manual PDF: {Ruta}", ruta);
            }

            var psi = new ProcessStartInfo
            {
                FileName = ruta,
                UseShellExecute = true
            };

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al intentar abrir el archivo de manual de usuario en la ruta: {Ruta}", ruta);
            return false;
        }
    }

    public string ObtenerRutaManual()
    {
        // 1. Ruta estándar en la carpeta de ejecución de la aplicación (Assets/Manual/)
        var rutaBase = AppContext.BaseDirectory;
        var rutaEnBin = Path.Combine(rutaBase, "Assets", "Manual", NombreArchivoManual);
        if (File.Exists(rutaEnBin))
        {
            return Path.GetFullPath(rutaEnBin);
        }

        // 2. Ruta alternativa en raíz de ejecución
        var rutaDirecta = Path.Combine(rutaBase, NombreArchivoManual);
        if (File.Exists(rutaDirecta))
        {
            return Path.GetFullPath(rutaDirecta);
        }

        // 3. Ruta en tiempo de desarrollo / test en árbol de fuentes
        var rutaFuentes = Path.GetFullPath(Path.Combine(rutaBase, "..", "..", "..", "..", "src", "Retail.App", "Assets", "Manual", NombreArchivoManual));
        if (File.Exists(rutaFuentes))
        {
            return rutaFuentes;
        }

        return Path.GetFullPath(rutaEnBin);
    }
}

