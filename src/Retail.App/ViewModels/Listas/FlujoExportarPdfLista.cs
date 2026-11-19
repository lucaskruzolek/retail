using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Retail.App.Services;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Flujo de "Exportar PDF" de una lista: pide la ruta, crea el archivo y, si está bloqueado (por ejemplo abierto en un
/// visor), informa y deja reintentar, igual que <c>FlujoExportarPdfPresupuesto</c>.
/// </summary>
public sealed class FlujoExportarPdfLista
{
    private readonly IListaDialogService _dialogService;
    private readonly ILogger _logger;

    public FlujoExportarPdfLista(IListaDialogService dialogService, ILogger logger)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Nombre que propone el cuadro de guardado: <c>Lista-3.º grado - Colegio.pdf</c>, sin caracteres inválidos.</summary>
    public static string NombreSugerido(string nombreLista)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var saneado = new StringBuilder(nombreLista.Length);
        foreach (var caracter in nombreLista.Trim())
        {
            saneado.Append(Array.IndexOf(invalidos, caracter) >= 0 ? '-' : caracter);
        }

        return $"Lista-{saneado}.pdf";
    }

    /// <summary>
    /// Pide la ruta y escribe el PDF. Devuelve <c>false</c> si el usuario canceló y <c>true</c> si el archivo quedó
    /// guardado. Cualquier error distinto de un problema de archivo se propaga, tras borrar el archivo a medias.
    /// </summary>
    public async Task<bool> EjecutarAsync(string nombreLista, Func<Stream, Task> escribir)
    {
        ArgumentNullException.ThrowIfNull(escribir);

        var ruta = _dialogService.ElegirRutaPdf(NombreSugerido(nombreLista));
        if (ruta is null)
        {
            return false;
        }

        while (true)
        {
            var archivoAbierto = false;

            try
            {
                // Ley 4: crear el archivo (E/S) no corre en el hilo de UI.
                await using var destino = await Task.Run(
                    () => new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true));
                archivoAbierto = true;

                await escribir(destino);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "No se pudo guardar el PDF de la lista «{Lista}» en {Ruta}: {Mensaje}", nombreLista, ruta, ex.Message);
                BorrarArchivoParcial(ruta, archivoAbierto);

                var reintentar = _dialogService.Confirmar(
                    "No se pudo guardar el PDF",
                    $"No se pudo guardar el archivo: {ex.Message}\n\nCierre el archivo si está abierto en otra aplicación. ¿Reintentar?");
                if (!reintentar)
                {
                    return false;
                }

                continue;
            }
            catch (Exception)
            {
                BorrarArchivoParcial(ruta, archivoAbierto);
                throw;
            }

            _dialogService.MostrarInformacion("PDF guardado", $"La lista se guardó en {ruta}");
            return true;
        }
    }

    /// <summary>Si el fallo ocurrió después de abrir el archivo queda uno a medias: se borra antes de reintentar.</summary>
    private void BorrarArchivoParcial(string ruta, bool archivoAbierto)
    {
        if (!archivoAbierto)
        {
            return;
        }

        try
        {
            File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "No se pudo borrar el archivo parcial {Ruta}", ruta);
        }
    }
}
