using Retail.Application.DTOs.Terminales;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para identificar y activar esta PC como terminal del local (MT-1). La activación se hace después del
/// ingreso normal y exige a un Gerente.
/// </summary>
public interface ITerminalService
{
    /// <summary>
    /// Determina si esta PC está activada: lee el id guardado en el equipo y lo contrasta con la base.
    /// </summary>
    Task<EstadoTerminalDto> VerificarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminales activas (no dadas de baja), ordenadas por nombre: las opciones para reemplazar un equipo.
    /// </summary>
    Task<IReadOnlyList<TerminalDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una terminal nueva con el nombre de esta PC y la guarda como la identidad local. Solo un Gerente.
    /// </summary>
    Task<TerminalDto> ActivarNuevaAsync(int idActor, string nombre, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambio de hardware: la terminal existente pasa a ser esta PC y conserva su historial. Solo un Gerente.
    /// </summary>
    Task<TerminalDto> ActivarReemplazoAsync(int idActor, int idTerminal, CancellationToken cancellationToken = default);
}
