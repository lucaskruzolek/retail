using Retail.Application.DTOs.Sistema;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto hacia el historial de migraciones de la base (RF-23). Permite verificar el esquema al arrancar y
/// actualizarlo desde la herramienta de comandos sin que Application dependa de EF Core.
/// </summary>
public interface IEsquemaBaseDatos
{
    Task<EstadoEsquemaDto> ObtenerEstadoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica las migraciones pendientes (crea la base si no existe) y devuelve los nombres de las aplicadas.
    /// </summary>
    Task<IReadOnlyList<string>> AplicarMigracionesPendientesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Última migración aplicada en la base, que se registra en cada copia para no restaurar un esquema
    /// desconocido. <c>null</c> si la base no tiene ninguna.
    /// </summary>
    Task<string?> ObtenerUltimaMigracionAplicadaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si la base ya tiene la tabla del historial de copias. Una base de una versión anterior no la tiene
    /// hasta migrar, pero igual debe poder respaldarse antes de hacerlo.
    /// </summary>
    Task<bool> ExisteHistorialDeBackupsAsync(CancellationToken cancellationToken = default);
}
