namespace Retail.Application.DTOs.Sesiones;

/// <summary>
/// Una fila de «quién está conectado y desde dónde» para la administración de sesiones.
/// </summary>
public record class SesionActivaDto
{
    public required int IdUsuario { get; init; }
    public required string NombreUsuario { get; init; }
    public required int IdTerminal { get; init; }
    public required string NombreTerminal { get; init; }
    public required DateTime InicioUtc { get; init; }
    public required DateTime UltimoLatidoUtc { get; init; }

    /// <summary>Verdadero si pasó el tiempo de vencimiento sin latido (calculado por el servidor de base de datos).</summary>
    public required bool Vencida { get; init; }
}
