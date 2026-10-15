using Retail.Application.Exceptions;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Valida y normaliza la ruta de un archivo de copia que elige el operador para restaurar.
/// </summary>
/// <remarks>
/// El archivo lo lee el servicio de SQL Server con su propia cuenta, no el usuario de Windows: una ruta UNC haría que
/// esa cuenta se conecte a otra máquina, y las rutas de dispositivo (<c>\\?\</c>, <c>\\.\</c>) saltean la
/// normalización de Windows. Solo se aceptan rutas locales a archivos <c>.bak</c>.
/// </remarks>
internal static class RutaCopiaBackup
{
    private const string ExtensionCopia = ".bak";

    /// <summary>
    /// Devuelve la ruta absoluta o lanza <see cref="RestauracionFallidaException"/> si no es una ruta aceptable.
    /// </summary>
    public static string Normalizar(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            throw new RestauracionFallidaException("Debe indicar la ruta del archivo de copia (.bak) a restaurar.");
        }

        var recortada = ruta.Trim();

        // Cubre UNC (\\servidor\recurso) y de dispositivo (\\?\ y \\.\), que también empiezan con dos barras.
        if (recortada.StartsWith(@"\\", StringComparison.Ordinal) || recortada.StartsWith("//", StringComparison.Ordinal))
        {
            throw new RestauracionFallidaException(
                "No se admiten rutas de red ni de dispositivo: copie el archivo .bak a una carpeta local de esta PC.");
        }

        string completa;
        try
        {
            completa = Path.GetFullPath(recortada);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new RestauracionFallidaException("La ruta del archivo de copia no es válida.", ex);
        }

        if (!string.Equals(Path.GetExtension(completa), ExtensionCopia, StringComparison.OrdinalIgnoreCase))
        {
            throw new RestauracionFallidaException("El archivo de copia debe tener la extensión .bak.");
        }

        return completa;
    }
}
