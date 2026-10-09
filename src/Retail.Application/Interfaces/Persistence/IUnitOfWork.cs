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
    /// Descarta todos los cambios pendientes en memoria (entidades agregadas o modificadas que no se guardaron).
    /// Se invoca cuando falla un guardado: como el contexto vive mientras dura la pantalla, sin esto los cambios
    /// rechazados se guardarían junto con la próxima operación exitosa.
    /// </summary>
    void DescartarCambios();
}
