using System.Runtime.InteropServices;
using System.Windows;

namespace Retail.App.Helpers;

/// <summary>
/// Copia texto al portapapeles sin lanzar excepciones: otra aplicación puede tenerlo bloqueado un instante
/// (<c>COMException</c> con <c>CLIPBRD_E_CANT_OPEN</c>), por eso se reintenta unas pocas veces antes de rendirse.
/// </summary>
public static class PortapapelesSeguro
{
    private const int Intentos = 3;
    private const int EsperaEntreIntentosMs = 50;

    /// <returns><c>true</c> si el texto quedó copiado; <c>false</c> si el portapapeles seguía bloqueado.</returns>
    public static bool TryCopiar(string texto)
    {
        for (var intento = 1; intento <= Intentos; intento++)
        {
            try
            {
                Clipboard.SetText(texto);
                return true;
            }
            catch (ExternalException)
            {
                if (intento < Intentos)
                {
                    Thread.Sleep(EsperaEntreIntentosMs);
                }
            }
        }

        return false;
    }
}
