using System.IO;
using System.Windows;
using Microsoft.Win32;
using Retail.Application.DTOs.Reportes;

namespace Retail.App.Services;

/// <summary>
/// Implementación de los diálogos de exportación con los cuadros estándar de Windows.
/// </summary>
public class ReporteDialogService : IReporteDialogService
{
    public string? ElegirArchivoDestino(string nombreSugerido, FormatoExportacionEnum formato)
    {
        var (filtro, extension) = formato switch
        {
            FormatoExportacionEnum.Excel => ("Libro de Excel (*.xlsx)|*.xlsx", ".xlsx"),
            FormatoExportacionEnum.Pdf => ("Documento PDF (*.pdf)|*.pdf", ".pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de exportación no soportado.")
        };

        var dialogo = new SaveFileDialog
        {
            Title = "Guardar reporte",
            FileName = nombreSugerido,
            Filter = filtro,
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = true
        };

        var propietario = System.Windows.Application.Current?.MainWindow is { IsVisible: true } ventana ? ventana : null;

        return dialogo.ShowDialog(propietario) == true ? dialogo.FileName : null;
    }

    public Stream AbrirArchivoDestino(string ruta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);

        // E/S asíncrona y sin compartir: ningún otro proceso lee un archivo a medio escribir.
        return new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
    }

    public void EliminarArchivoParcial(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Limpieza de mejor esfuerzo: el archivo se vuelve a truncar si se reintenta.
        }
    }

    public bool ConfirmarReintento(string mensaje)
    {
        return MessageBox.Show(mensaje, "No se pudo guardar el reporte", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void MostrarExito(string mensaje)
    {
        MessageBox.Show(mensaje, "Reporte guardado", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
