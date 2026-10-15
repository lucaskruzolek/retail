using Retail.Application.DTOs.Backups;
using Retail.Domain.Enums;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto hacia el directorio dedicado de respaldos (D-15): nombres de archivo, manifiestos y borrado por retención.
/// </summary>
public interface IArchivosBackup
{
    /// <summary>
    /// Devuelve la ruta para una copia nueva. Crea el directorio si no existe y verifica que se pueda escribir y que
    /// haya espacio libre suficiente: si algo falla, el error dice qué y dónde, en lugar del mensaje del motor.
    /// </summary>
    string PrepararRutaNueva(TipoBackupEnum tipo);

    /// <summary>
    /// Guarda el manifiesto como <c>.json</c> junto al archivo <c>.bak</c>.
    /// </summary>
    Task EscribirManifiestoAsync(string rutaArchivo, ManifiestoBackupDto manifiesto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lee el manifiesto <c>.json</c> que acompaña al archivo <c>.bak</c>. Lanza una excepción si el archivo o su
    /// manifiesto no existen o no se pueden leer.
    /// </summary>
    Task<ManifiestoBackupDto> LeerManifiestoAsync(string rutaArchivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tamaño y SHA-256 actuales del archivo, para comprobar que no cambió desde que se verificó.
    /// </summary>
    Task<ArchivoBackupDto> CalcularHuellaAsync(string rutaArchivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve una ruta dentro del directorio de respaldos desde la cual el motor puede restaurar la copia. Con SQL
    /// Server Express, <c>RESTORE</c> lo ejecuta la cuenta del servicio, que no puede leer carpetas del usuario (por
    /// ejemplo, Descargas). Si el <c>.bak</c> ya está en el directorio se devuelve sin cambios; si no, se copia junto a su
    /// manifiesto <c>.json</c> (con el permiso del usuario que la pide). Si ya hay otro archivo con el mismo nombre y
    /// distinto contenido, la copia lleva un sufijo numérico; si el contenido es idéntico, se reutiliza.
    /// </summary>
    Task<string> PrepararParaRestaurarAsync(string rutaArchivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra el archivo de la copia y su manifiesto, si existen.
    /// </summary>
    void EliminarCopia(string rutaArchivo);
}
