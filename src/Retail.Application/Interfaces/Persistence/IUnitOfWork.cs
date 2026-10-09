namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato para la coordinación atómica de transacciones y persistencia (Unit of Work).
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Confirma los cambios pendientes en una única transacción.
    /// Lanza <see cref="Exceptions.ConflictoDeConcurrenciaException"/> si otro usuario modificó los mismos datos.
    /// Si el guardado falla por cualquier motivo, descarta los cambios pendientes antes de relanzar el error: el
    /// llamador no necesita invocar <see cref="DescartarCambios"/> para ese caso.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarta todos los cambios pendientes en memoria y deja de seguir las entidades cargadas.
    /// Como el contexto vive mientras dura la pantalla, un caso de uso que modifica un agregado y después falla
    /// (sin llegar a guardar) lo invoca para que esos cambios no se guarden con la próxima operación exitosa.
    /// Después de un guardado exitoso libera las entidades para que la próxima operación lea datos frescos (D-15).
    /// </summary>
    void DescartarCambios();
}
