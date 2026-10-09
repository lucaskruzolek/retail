namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato para la coordinación atómica de transacciones y persistencia (Unit of Work).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Confirma los cambios pendientes en una única transacción.
    /// Lanza <see cref="Exceptions.ConflictoDeConcurrenciaException"/> si otro usuario modificó los mismos datos.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta todos los cambios pendientes en memoria y deja de seguir las entidades cargadas.
    /// Se invoca cuando falla un guardado: como el contexto vive mientras dura la pantalla, sin esto los cambios
    /// rechazados se guardarían junto con la próxima operación exitosa. Después de un guardado exitoso libera las
    /// entidades para que la próxima operación lea datos frescos (D-15).
    /// </summary>
    void DescartarCambios();
}
