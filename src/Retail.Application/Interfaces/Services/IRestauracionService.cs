using Retail.Application.DTOs.Backups;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Restauración de una copia completa de la base (RF-22, RF-23). Las dos entradas comparten un único mecanismo (base
/// temporal + intercambio, D-17 y D-21) y difieren solo en cómo se autoriza al Gerente.
/// </summary>
public interface IRestauracionService
{
    /// <summary>
    /// Desde la aplicación: el Gerente logueado se reautentica contra la base vigente (D-8) y elige una copia
    /// <c>Verificado</c> del historial.
    /// </summary>
    Task<ResultadoRestauracionDto> RestaurarDesdeHistorialAsync(
        int idActor,
        string password,
        Guid referenciaCopia,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recuperación ante desastres (<c>Retail.Cli restaurar</c>): la base vigente puede no existir o estar dañada, así
    /// que la credencial se verifica contra un Gerente activo <b>dentro de la copia</b> (D-18). El archivo debe tener
    /// su manifiesto.
    /// </summary>
    Task<ResultadoRestauracionDto> RestaurarDesdeArchivoAsync(
        string rutaArchivo,
        string nombreUsuario,
        string password,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Última restauración registrada, o <c>null</c> si nunca se restauró. Es de solo lectura y la consultan el Gerente
    /// y el Administrador: la aplicación la muestra para que una restauración no pase inadvertida (R-20).
    /// </summary>
    Task<RegistroRestauracionDto?> ObtenerUltimaRestauracionAsync(int idActor, CancellationToken cancellationToken = default);
}
