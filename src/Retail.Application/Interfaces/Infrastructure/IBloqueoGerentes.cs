namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Bloqueo entre terminales que serializa las operaciones capaces de dejar al sistema sin ningún Gerente activo
/// (dar de baja a un Gerente o quitarle el rol), válido también entre procesos y PCs distintos.
/// </summary>
/// <remarks>
/// La regla "siempre queda al menos un Gerente" abarca un <b>conjunto</b> de filas, no una sola. Si quedan dos Gerentes y
/// dos terminales dan de baja a uno distinto cada una, ambas cuentan dos Gerentes, ambas pasan la validación y ambas
/// guardan: queda cero. Es un <i>write skew</i>. Un <c>rowversion</c> no lo evita porque cada terminal modifica una fila
/// diferente y ninguna choca con la otra. La solución es tomar este bloqueo antes de contar y soltarlo recién después de
/// guardar, de modo que la segunda terminal cuente ya con el cambio de la primera. Si el bloqueo no se obtiene tras una
/// espera breve, se lanza <c>ConflictoDeConcurrenciaException</c>.
/// </remarks>
public interface IBloqueoGerentes
{
    /// <summary>
    /// Toma el bloqueo exclusivo. Dura hasta liberar el objeto devuelto, que debe liberarse luego de guardar los cambios.
    /// </summary>
    Task<IAsyncDisposable> AdquirirAsync(CancellationToken cancellationToken = default);
}
