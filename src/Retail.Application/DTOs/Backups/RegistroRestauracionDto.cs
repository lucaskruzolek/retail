using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Backups;

/// <summary>
/// Restauración de la base, para que la aplicación la muestre (IU-10, R-20): un restore equivocado o malicioso
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

    /// <summary>
    /// Administrador que ejecutó la restauración cuando no fue el Gerente que la autorizó (control dual, D-30);
    /// <c>null</c> si la ejecutó el mismo Gerente o la consola.
    /// </summary>
    public string? NombreOperador { get; init; }

    public required Guid ReferenciaCopia { get; init; }

    public required string RutaArchivo { get; init; }

    /// <summary>
    /// Indica si antes de restaurar se tomó una copia previa de la base vigente (no la hay si no existía una base vigente
    /// o, desde la consola, si la copia previa falló).
    /// </summary>
    public bool TuvoCopiaPrevia { get; init; }

    /// <summary>
    /// Nombre con el que quedó la base reemplazada, o <c>null</c> si no había una base vigente.
    /// </summary>
    public string? BaseAnterior { get; init; }
}
