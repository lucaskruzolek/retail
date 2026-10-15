using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Fila del historial de copias de seguridad para la pantalla de respaldos (IU-10).
/// </summary>
public sealed record RegistroBackupDto
{
    public required int IdRegistroBackup { get; init; }

    public required Guid Referencia { get; init; }

    public required TipoBackupEnum Tipo { get; init; }

    public required EstadoBackupEnum Estado { get; init; }

    /// <summary>
    /// Nombre de usuario de quien la pidió, o "Sistema".
    /// </summary>
    public required string Operador { get; init; }

    public required DateTime FechaInicio { get; init; }

    public DateTime? FechaFin { get; init; }

    public required string RutaArchivo { get; init; }

    public long? TamanoBytes { get; init; }

    public string? MensajeError { get; init; }

    public required bool EsRestaurable { get; init; }
}
