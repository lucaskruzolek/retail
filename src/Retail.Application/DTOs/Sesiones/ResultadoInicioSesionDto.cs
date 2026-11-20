namespace Retail.Application.DTOs.Sesiones;

/// <summary>
/// Resultado de intentar registrar la sesión de un usuario en una terminal (MT-3).
/// </summary>
public record class ResultadoInicioSesionDto
{
    /// <summary>Verdadero si la sesión quedó registrada para esta terminal.</summary>
    public required bool Iniciada { get; init; }

    /// <summary>Si se rechazó: nombre de la terminal donde el usuario ya tiene una sesión vigente.</summary>
    public string? TerminalOcupada { get; init; }

    /// <summary>Si se rechazó: momento (UTC, hora del servidor) en que empezó la otra sesión.</summary>
    public DateTime? InicioOtraSesionUtc { get; init; }

    /// <summary>Verdadero si para entrar se tomó una sesión vencida de otra terminal (la UI puede avisarlo).</summary>
    public bool TomoSesionVencida { get; init; }
}
