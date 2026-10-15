namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Bloqueo de lectores y escritor entre restauraciones y copias, válido también entre procesos (la aplicación,
/// <c>Retail.Cli</c> y la tarea programada). Los métodos exclusivos no esperan: si el bloqueo no está disponible, fallan
/// en el acto con un mensaje claro. El compartido espera unos segundos (ver <c>AdquirirCompartidoAsync</c>).
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
    /// Intenta tomar el mismo bloqueo exclusivo, pero devuelve <c>null</c> en lugar de fallar si no está disponible
    /// (hay una restauración o una copia en curso). Si lo obtiene, ninguna copia está corriendo: todo registro
    /// <c>EnCurso</c> de la base vigente quedó huérfano, porque el motor suelta el bloqueo compartido de una copia cuando
    /// su proceso muere (B-0). Quien lo obtiene debe liberarlo.
    /// </summary>
    Task<IAsyncDisposable?> IntentarAdquirirExclusivoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo toman las copias (manual, automática y previa a migrar). Espera unos segundos: otra copia que se está iniciando
    /// toma el exclusivo por un instante para cerrar huérfanos (B-0) y no debe hacer fallar a esta. Falla con
    /// <c>BackupFallidoException</c> si pasado ese tiempo hay una restauración en curso (o el instante persiste). La copia
    /// previa a restaurar no lo toma: corre bajo el bloqueo exclusivo de su restauración.
    /// </summary>
    Task<IAsyncDisposable> AdquirirCompartidoAsync(CancellationToken cancellationToken = default);
}
