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
    /// Borra el archivo de la copia y su manifiesto, si existen.
    /// </summary>
    void EliminarCopia(string rutaArchivo);
}
