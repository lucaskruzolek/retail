using Retail.Application.DTOs.Auth;

namespace Retail.Application.Interfaces.Persistence;

/// <summary>
/// Contrato para interactuar con la persistencia transaccional y auditoría de inicios de sesión (sp_RegistrarIntentoLogin).
/// </summary>
public interface IRegistroAccesosService
{
    Task<ResultadoRegistroIntentoDto> RegistrarIntentoAsync(
        string nombreUsuario,
        int? idUsuario,
        bool credencialesValidas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un hecho en AUDITORIA_ACCESOS sin tocar contadores ni bloqueos: esos los decide el dominio (D-31) o,
    /// para el inicio de sesión, el procedimiento almacenado. Nunca debe recibir contraseñas.
    /// </summary>
    Task RegistrarEventoAsync(
        TipoEventoAccesoEnum tipo,
        ResultadoEventoAccesoEnum resultado,
        string nombreUsuario,
        int? idUsuario,
        string? detalle,
        CancellationToken cancellationToken = default);
}
