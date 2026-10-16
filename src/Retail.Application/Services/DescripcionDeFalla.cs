using Retail.Application.Exceptions;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Texto que ve el operador cuando una copia o una restauración fallan por una causa técnica (B-1).
/// </summary>
/// <remarks>
/// Los errores de archivo o de disco traen mensajes del sistema operativo que el operador entiende ("no se encuentra el
/// archivo", "acceso denegado") y se muestran tal cual. Los del motor de base de datos traen nombres de bases, rutas
/// internas y códigos que no ayudan a quien opera la caja: se reemplazan por una indicación. El detalle técnico no se
/// pierde: viaja como <c>InnerException</c> y la consola y la aplicación lo registran en el log.
/// </remarks>
internal static class DescripcionDeFalla
{
    public static string Para(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        // Las excepciones propias de la aplicación y del dominio ya traen un mensaje pensado para el operador.
        return ex is IOException or UnauthorizedAccessException
            or BackupFallidoException or RestauracionFallidaException or DomainException
            ? ex.Message
            : "error del motor de base de datos o del sistema (el detalle técnico quedó en el registro de la aplicación)";
    }
}
