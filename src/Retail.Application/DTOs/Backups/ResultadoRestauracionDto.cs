namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Resultado de una restauración terminada. Después de recibirlo, la aplicación debe reiniciarse: sus conexiones
/// apuntaban a la base reemplazada.
/// </summary>
public sealed record ResultadoRestauracionDto
{
    /// <summary>
    /// Advertencia obligatoria de RF-22 (R-3): la base restaurada puede no tener comprobantes que ARCA ya autorizó.
    /// </summary>
    public const string AvisoConciliacionArca =
        "La base volvió al estado de la copia. Los comprobantes autorizados por ARCA después de esa fecha no figuran en el " +
        "sistema: concilie la numeración con el último comprobante autorizado antes de volver a facturar. " +
        "La restauración no anula ventas ante ARCA.";

    public required Guid ReferenciaRestauracion { get; init; }

    public required Guid ReferenciaCopia { get; init; }

    /// <summary>
    /// Copia <c>PreRestauracion</c> de la base reemplazada, o <c>null</c> si no se pudo tomar (D-20).
    /// </summary>
    public Guid? ReferenciaCopiaPrevia { get; init; }

    /// <summary>
    /// Nombre con el que quedó la base reemplazada, o <c>null</c> si no había una base vigente.
    /// </summary>
    public string? BaseAnterior { get; init; }

    /// <summary>
    /// Migraciones que se aplicaron a la copia para ponerla al día con esta versión.
    /// </summary>
    public IReadOnlyList<string> MigracionesAplicadas { get; init; } = [];

    /// <summary>
    /// Situaciones que no impidieron la restauración pero que el operador debe conocer.
    /// </summary>
    public IReadOnlyList<string> Advertencias { get; init; } = [];
}
