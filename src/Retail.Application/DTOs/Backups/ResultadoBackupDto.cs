namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Resultado de una copia terminada y verificada.
/// </summary>
public sealed record ResultadoBackupDto
{
    public required Guid Referencia { get; init; }

    public required string RutaArchivo { get; init; }

    public required long TamanoBytes { get; init; }

    public required string HashSha256 { get; init; }

    /// <summary>
    /// Falso si la base todavía no tenía la tabla del historial: la copia existe (archivo + manifiesto) pero no
    /// figura en REGISTROS_BACKUP.
    /// </summary>
    public required bool RegistradaEnHistorial { get; init; }

    /// <summary>
    /// Archivos de copias viejas que la política de retención borró en esta ejecución.
    /// </summary>
    public IReadOnlyList<string> CopiasPurgadas { get; init; } = [];

    /// <summary>
    /// Copias que la retención no pudo borrar (por ejemplo, un archivo abierto). No invalidan la copia nueva:
    /// se informan para que quien llama las registre o las muestre.
    /// </summary>
    public IReadOnlyList<string> ErroresDeRetencion { get; init; } = [];
}
