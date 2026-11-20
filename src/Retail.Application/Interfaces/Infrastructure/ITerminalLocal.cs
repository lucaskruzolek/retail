namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Identidad de esta PC como terminal del local: qué terminal de la base es (si ya fue activada) y cómo se llama el
/// equipo en Windows. El dato vive en un archivo local de la PC, no en la base: la base la comparten todas las
/// terminales y por eso no puede decir cuál de ellas es "esta".
/// </summary>
public interface ITerminalLocal
{
    /// <summary>
    /// Id de la terminal guardado en esta PC, o <c>null</c> si todavía no fue activada (o el archivo no se pudo leer).
    /// </summary>
    int? IdTerminal { get; }

    /// <summary>
    /// Nombre de Windows de esta PC (<c>Environment.MachineName</c>). Solo sirve de control: no identifica la terminal.
    /// </summary>
    string NombreEquipo { get; }

    /// <summary>
    /// Guarda en esta PC el id de la terminal que se acaba de activar.
    /// </summary>
    Task GuardarAsync(int idTerminal, CancellationToken cancellationToken = default);
}
