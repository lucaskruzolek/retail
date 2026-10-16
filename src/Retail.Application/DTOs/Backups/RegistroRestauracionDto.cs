using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Última restauración de la base, para que la aplicación la muestre (IU-10, R-20): un restore equivocado o malicioso
/// debe poder detectarse aunque nadie lo haya anunciado.
/// </summary>
public sealed record RegistroRestauracionDto
{
    public required Guid Referencia { get; init; }

    /// <summary>
    /// Momento de la restauración, en UTC.
    /// </summary>
    public required DateTime Fecha { get; init; }

    public required OrigenRestauracionEnum Origen { get; init; }

    /// <summary>
    /// Gerente que autorizó la restauración.
    /// </summary>
    public required string NombreUsuario { get; init; }

    public required Guid ReferenciaCopia { get; init; }

    public required string RutaArchivo { get; init; }

    /// <summary>
    /// Nombre con el que quedó la base reemplazada, o <c>null</c> si no había una base vigente.
    /// </summary>
    public string? BaseAnterior { get; init; }
}
