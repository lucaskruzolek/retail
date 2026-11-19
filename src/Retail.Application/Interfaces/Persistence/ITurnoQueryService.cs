using Retail.Application.DTOs.Caja;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Consultas de solo lectura sobre turnos de caja abiertos: proyecciones <c>AsNoTracking</c> a DTOs, evaluadas en SQL
/// Server (Ley 8). Las modificaciones pasan siempre por la raíz <c>TurnoCaja</c> (Ley 2).
/// </summary>
public interface ITurnoQueryService
{
    /// <summary>
    /// El turno abierto de una terminal con el nombre del cajero y de la terminal, o <c>null</c> si no tiene ninguno.
    /// </summary>
    Task<TurnoCajaDto?> ObtenerAbiertoDeTerminalAsync(int idTerminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// El turno abierto de un cajero (en cualquier terminal), o <c>null</c> si no tiene ninguno.
    /// </summary>
    Task<TurnoCajaDto?> ObtenerAbiertoDeUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos los turnos abiertos de todas las terminales, ordenados por terminal.
    /// </summary>
    Task<IReadOnlyList<TurnoAbiertoDto>> ListarAbiertosAsync(CancellationToken cancellationToken = default);
}
