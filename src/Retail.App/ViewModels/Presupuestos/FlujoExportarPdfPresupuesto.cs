using System.IO;
using Microsoft.Extensions.Logging;
using Retail.App.Services;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Flujo de "Exportar PDF" de un presupuesto, compartido por la grilla, su detalle y (en P5) el diálogo de guardado del
/// Punto de venta: pide la ruta, crea el archivo y, si está bloqueado (por ejemplo abierto en un visor), informa y deja
/// reintentar sin perder la ruta elegida, igual que la exportación de reportes (RF-26).
/// </summary>
public sealed class FlujoExportarPdfPresupuesto
{
    private readonly IPresupuestoDialogService _dialogService;
    private readonly ILogger _logger;

    public FlujoExportarPdfPresupuesto(IPresupuestoDialogService dialogService, ILogger logger)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Nombre que propone el cuadro de guardado: <c>Presupuesto-00000123.pdf</c>.</summary>
    public static string NombreSugerido(int idPresupuesto)
    {
        return $"Presupuesto-{PresentacionPresupuesto.FormatearNumero(idPresupuesto)}.pdf";
    }

    /// <summary>
    /// Pide la ruta y escribe el PDF. Devuelve <c>false</c> si el usuario canceló (la elección de ruta o el reintento) y
    /// <c>true</c> si el archivo quedó guardado. Cualquier otro error (distinto de un problema de archivo) se propaga a
    /// quien llama, después de borrar el archivo a medias.
    /// </summary>
    /// <param name="idPresupuesto">Presupuesto a exportar; define el nombre sugerido.</param>
    /// <param name="escribir">Escribe el PDF en el destino. Quien llama decide cómo accede a los datos (por ejemplo, en
    /// la fila de accesos de la pantalla) y el flujo solo se ocupa del archivo.</param>
    public async Task<bool> EjecutarAsync(int idPresupuesto, Func<Stream, Task> escribir)
    {
        ArgumentNullException.ThrowIfNull(escribir);

        var ruta = _dialogService.ElegirRutaPdf(NombreSugerido(idPresupuesto));
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
                _logger.LogWarning(ex, "No se pudo guardar el PDF del presupuesto {IdPresupuesto} en {Ruta}: {Mensaje}", idPresupuesto, ruta, ex.Message);
                BorrarArchivoParcial(ruta, archivoAbierto);

                var reintentar = _dialogService.Confirmar(
                    "No se pudo guardar el PDF",
                    $"No se pudo guardar el archivo: {ex.Message}\n\nCerrá el archivo si está abierto en otra aplicación. ¿Reintentar?");
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

            _dialogService.MostrarInformacion("PDF guardado", $"El presupuesto se guardó en {ruta}");
            return true;
        }
    }

    /// <summary>
    /// Si el fallo ocurrió DESPUÉS de abrir el archivo queda uno a medias: se borra antes de ofrecer el reintento. Si
    /// falló al ABRIRLO (está en uso), el archivo existente no es nuestro y no se toca.
    /// </summary>
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
            // Limpieza de mejor esfuerzo: el archivo se vuelve a truncar si se reintenta.
            _logger.LogWarning(ex, "No se pudo borrar el archivo parcial {Ruta}", ruta);
        }
    }
}
