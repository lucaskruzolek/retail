using Retail.Application.DTOs.Backups;
using Retail.Domain.Enums;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Copias de seguridad completas de la base (RF-22, RNF-08).
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Historial de copias, de la más reciente a la más antigua. Lo consultan el Gerente y el Administrador.
    /// </summary>
    Task<IReadOnlyList<RegistroBackupDto>> ListarAsync(int idActor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copia pedida por un Gerente o un Administrador (D-32), que debe reautenticarse con su propia contraseña (D-8).
    /// </summary>
    Task<ResultadoBackupDto> CrearBackupManualAsync(
        int idActor,
        string password,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copia que dispara el sistema: automática o previa a migrar. No recibe actor: hacer una copia no da acceso
    /// a ninguna cuenta ni modifica datos, así que puede ejecutarla la herramienta de comandos (D-13).
    /// </summary>
    Task<ResultadoBackupDto> CrearBackupDelSistemaAsync(
        TipoBackupEnum tipo,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copia de la base vigente justo antes de reemplazarla (D-20). La invoca <see cref="IRestauracionService"/> cuando
    /// el Gerente ya fue autorizado; el operador queda registrado como foto. Igual que las copias del sistema, no da
    /// acceso a ninguna cuenta ni modifica datos.
    /// </summary>
    Task<ResultadoBackupDto> CrearBackupPreRestauracionAsync(
        int idUsuario,
        string nombreUsuario,
        IProgress<int>? progreso = null,
        CancellationToken cancellationToken = default);
}
