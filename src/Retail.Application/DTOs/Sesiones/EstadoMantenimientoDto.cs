namespace Retail.Application.DTOs.Sesiones;

/// <summary>
/// Estado del aviso de mantenimiento programado (la fila única de <c>MANTENIMIENTO_PROGRAMADO</c>).
/// </summary>
public record class EstadoMantenimientoDto
{
    /// <summary>Momento (UTC) previsto, o nulo si no hay mantenimiento programado.</summary>
    public DateTime? ProgramadoParaUtc { get; init; }

    public string? Motivo { get; init; }

    /// <summary>Hora (UTC) del servidor de base de datos.</summary>
    public required DateTime AhoraServidorUtc { get; init; }

    public bool HayMantenimiento => ProgramadoParaUtc is not null;
}
