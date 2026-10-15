using System.Security.Cryptography;
using Retail.Application.DTOs.Backups;

namespace Retail.Infrastructure.Backups;

/// <summary>
/// Tamaño y SHA-256 de un archivo, calculados por streaming para no cargar una copia grande en memoria (RNF-03).
/// </summary>
internal static class HuellaArchivo
{
    public static async Task<ArchivoBackupDto> CalcularAsync(string rutaArchivo, CancellationToken cancellationToken)
    {
        await using var archivo = new FileStream(
            rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        var hash = await SHA256.HashDataAsync(archivo, cancellationToken);
        return new ArchivoBackupDto(archivo.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }
}
