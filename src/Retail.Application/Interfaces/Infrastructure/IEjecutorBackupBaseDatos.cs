using Retail.Application.DTOs.Backups;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto hacia el motor de base de datos para copias completas (RF-22). La implementación usa las sentencias
/// nativas de SQL Server: Application no conoce <c>BACKUP DATABASE</c> ni el formato <c>.bak</c>.
/// </summary>
public interface IEjecutorBackupBaseDatos
{
    /// <summary>
    /// Escribe una copia completa de la base en <paramref name="rutaArchivo"/> con verificación de páginas
    /// (<c>CHECKSUM</c>) y devuelve su tamaño y su hash SHA-256.
    /// </summary>
    /// <param name="progreso">Porcentaje completado (0-100), si el llamador quiere mostrarlo.</param>
    Task<ArchivoBackupDto> CrearCopiaAsync(string rutaArchivo, IProgress<int>? progreso, CancellationToken cancellationToken = default);

    /// <summary>
    /// Comprueba que el archivo sea legible y restaurable sin restaurarlo. Lanza una excepción si no lo es.
    /// </summary>
    Task VerificarCopiaAsync(string rutaArchivo, CancellationToken cancellationToken = default);
}
