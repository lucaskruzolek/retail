namespace Retail.Application.DTOs.Sistema;

/// <summary>
/// Situación del esquema de la base respecto de las migraciones que conoce esta versión de la aplicación.
/// </summary>
public record class EstadoEsquemaDto
{
    /// <summary>
    /// Falso si el servidor no responde o la base todavía no existe.
    /// </summary>
    public required bool PuedeConectar { get; init; }

    /// <summary>
    /// Migraciones de esta versión que la base todavía no tiene: la base está desactualizada.
    /// </summary>
    public required IReadOnlyList<string> MigracionesPendientes { get; init; }

    /// <summary>
    /// Migraciones aplicadas en la base que esta versión no conoce: la base es más nueva que la aplicación
    /// (por ejemplo, se restauró un respaldo de una versión posterior).
    /// </summary>
    public required IReadOnlyList<string> MigracionesDesconocidas { get; init; }

    public bool EstaAlDia => PuedeConectar && MigracionesPendientes.Count == 0 && MigracionesDesconocidas.Count == 0;
}
