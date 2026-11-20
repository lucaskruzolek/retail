namespace Retail.Application.DTOs.Sesiones;

/// <summary>
/// Respuesta a un latido: si la sesión sigue siendo de esta terminal y qué mantenimiento hay programado. Se lee todo en
/// una sola ida a la base y con la hora del servidor.
/// </summary>
public record class EstadoLatidoDto
{
    /// <summary>Falso si la sesión fue tomada por otra terminal (por vencimiento) o cerrada por un Gerente.</summary>
    public required bool SesionVigente { get; init; }

    /// <summary>Momento (UTC) del mantenimiento programado, o nulo si no hay ninguno.</summary>
    public DateTime? MantenimientoProgramadoParaUtc { get; init; }

    public string? MotivoMantenimiento { get; init; }

    /// <summary>Hora (UTC) del servidor de base de datos: permite calcular «en N minutos» sin confiar en el reloj local.</summary>
    public required DateTime AhoraServidorUtc { get; init; }
}
