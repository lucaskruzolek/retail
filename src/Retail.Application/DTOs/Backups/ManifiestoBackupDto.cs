namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Descripción de una copia que se guarda como <c>.json</c> junto al <c>.bak</c> (R-1, R-2).
/// </summary>
/// <remarks>
/// El historial vive dentro de la base que se respalda: si la base se pierde, el historial también. El manifiesto
/// permite reconstruir qué contiene cada archivo (fecha, esquema, hash) con solo mirar la carpeta de respaldos.
/// </remarks>
public sealed record ManifiestoBackupDto
{
    public required Guid Referencia { get; init; }

    public required string Tipo { get; init; }

    public required DateTime FechaInicioUtc { get; init; }

    /// <summary>
    /// Nombre de usuario de quien la pidió, o "Sistema".
    /// </summary>
    public required string Operador { get; init; }

    public required string ArchivoBak { get; init; }

    public required long TamanoBytes { get; init; }

    public required string HashSha256 { get; init; }

    public required string UltimaMigracion { get; init; }

    public required string VersionApp { get; init; }

    /// <summary>
    /// Falso si la copia se tomó antes de que existiera la tabla del historial (migración desde una versión vieja).
    /// </summary>
    public required bool RegistradaEnHistorial { get; init; }
}
