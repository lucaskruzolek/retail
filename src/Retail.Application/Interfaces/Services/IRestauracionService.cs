using Retail.Application.DTOs.Backups;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Restauración de una copia completa de la base (RF-22, RF-23). Las tres entradas comparten un único mecanismo (base
/// temporal + intercambio, D-17 y D-21) y difieren solo en cómo se autoriza la operación.
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
    /// Control dual (D-30) con step-up (D-33): un Administrador ejecuta la restauración, reingresa su propia contraseña y un
    /// Gerente activo, sin cambio de contraseña pendiente, la autoriza con su credencial, que se verifica contra la base
    /// vigente. Cada intento del Administrador se cuenta antes de verificar (falle la credencial que falle) y la cuenta se
    /// bloquea al agotarlos (D-31).
    /// </summary>
    Task<ResultadoRestauracionDto> RestaurarConAutorizacionDeGerenteAsync(
        int idActor,
        string passwordActor,
        CredencialAutorizanteDto autorizante,
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

    /// <summary>
    /// Historial de restauraciones, de la más reciente a la más antigua (D-34). Es de solo lectura, sin paginación (las
    /// restauraciones son excepcionales) y lo consultan el Gerente y el Administrador; cualquier otro rol recibe
    /// <c>OperacionNoAutorizadaException</c>.
    /// </summary>
    Task<IReadOnlyList<RegistroRestauracionDto>> ListarRestauracionesAsync(int idActor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cantidad de terminales, distintas de esta, con una sesión vigente (MT-12). La interfaz la usa antes de restaurar para
    /// esperar a que las demás terminales hayan salido. Lo consultan el Gerente y el Administrador.
    /// </summary>
    /// <remarks>
    /// Contrato del mantenimiento: la interfaz programa el aviso (<c>ISesionService.ProgramarMantenimientoAsync</c>), espera
    /// a que este conteo llegue a cero y recién entonces llama a una de las dos restauraciones de la aplicación. Si esa
    /// restauración falla o se cancela, el servicio cancela el aviso por su cuenta; si tiene éxito, el aviso queda en la base
    /// anterior y la restaurada arranca sin aviso (salvo que la copia lo trajera).
    /// </remarks>
    Task<int> ContarTerminalesConectadasAsync(int idActor, CancellationToken cancellationToken = default);
}
