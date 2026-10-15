namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Resultado físico de escribir una copia: lo necesario para comprobar más adelante que el archivo no cambió.
/// </summary>
public sealed record ArchivoBackupDto(long TamanoBytes, string HashSha256);
