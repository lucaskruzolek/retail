using Retail.Application.DTOs.Sesiones;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Casos de uso de la sesión única por usuario (MT-3) y del mantenimiento programado (MT-12). Iniciar, renovar y cerrar
/// los usa cualquier usuario; listar, forzar el cierre y el mantenimiento son solo de un Gerente.
/// </summary>
public interface ISesionService
{
    Task<ResultadoInicioSesionDto> IniciarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default);

    Task<EstadoLatidoDto> RenovarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default);

    Task CerrarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default);

    /// <summary>Sesiones abiertas y desde dónde. Solo un Gerente.</summary>
    Task<IReadOnlyList<SesionActivaDto>> ListarAsync(int idActor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra la sesión de otro usuario (por ejemplo, una PC colgada). Solo un Gerente; no puede cerrar la propia.
    /// </summary>
    Task<bool> ForzarCierreAsync(int idActor, int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Avisa un mantenimiento para dentro de 0 a 60 minutos. Solo un Gerente.</summary>
    Task ProgramarMantenimientoAsync(int idActor, int minutosDesdeAhora, string? motivo, CancellationToken cancellationToken = default);

    /// <summary>Cancela el aviso de mantenimiento. Solo un Gerente.</summary>
    Task CancelarMantenimientoAsync(int idActor, CancellationToken cancellationToken = default);
}
