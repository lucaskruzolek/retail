namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Bloqueo de lectores y escritor entre restauraciones y copias, válido también entre procesos (la aplicación,
/// <c>Retail.Cli</c> y la tarea programada). Ninguno de los dos métodos espera: si el bloqueo no está disponible,
/// fallan en el acto con un mensaje claro.
/// </summary>
/// <remarks>
/// Una restauración reemplaza la base completa: una copia que corre en paralelo copiaría una base a punto de
/// desaparecer, y el intercambio cortaría su conexión. Las copias entre sí ya se excluyen por el índice de copia única
/// en curso (R-12), así que comparten el bloqueo.
/// </remarks>
public interface IBloqueoRestauracion
{
    /// <summary>
    /// Lo toma la restauración. Falla si hay otra restauración o una copia en curso. Dura hasta liberar el objeto
    /// devuelto.
    /// </summary>
    Task<IAsyncDisposable> AdquirirExclusivoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo toman las copias (manual, automática y previa a migrar). Falla con <c>BackupFallidoException</c> si hay una
    /// restauración en curso. La copia previa a restaurar no lo toma: corre bajo el bloqueo exclusivo de su restauración.
    /// </summary>
    Task<IAsyncDisposable> AdquirirCompartidoAsync(CancellationToken cancellationToken = default);
}
