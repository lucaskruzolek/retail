using Retail.Application.DTOs.Sesiones;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Puerto de las sesiones activas (una por usuario, MT-3) y del aviso de mantenimiento (MT-12). Es estado técnico
/// efímero, no un registro de negocio: no es una entidad del Dominio y sus filas se borran físicamente (excepción
/// documentada a la Ley 3). Todos los tiempos los pone el servidor de base de datos, nunca el reloj de la terminal.
/// </summary>
public interface ISesionesActivas
{
    /// <summary>
    /// Registra la sesión del usuario en la terminal, de forma atómica. Rechaza si el usuario tiene una sesión vigente
    /// en otra terminal; si esa sesión está vencida, la toma.
    /// </summary>
    Task<ResultadoInicioSesionDto> IniciarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Latido: renueva la sesión y, en el mismo viaje, lee el aviso de mantenimiento y la hora del servidor.
    /// </summary>
    Task<EstadoLatidoDto> RenovarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra la sesión exacta (usuario y terminal). Si otra terminal ya la tomó, no hace nada.
    /// </summary>
    Task CerrarAsync(int idUsuario, int idTerminal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SesionActivaDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra la sesión del usuario, esté donde esté. Devuelve si había una.
    /// </summary>
    Task<bool> ForzarCierreAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Programa el mantenimiento para dentro de <paramref name="minutosDesdeAhora"/> minutos (hora del servidor).
    /// </summary>
    Task ProgramarMantenimientoAsync(int minutosDesdeAhora, string? motivo, int idUsuario, CancellationToken cancellationToken = default);

    Task CancelarMantenimientoAsync(CancellationToken cancellationToken = default);

    Task<EstadoMantenimientoDto> ObtenerMantenimientoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cantidad de sesiones vigentes (no vencidas) de terminales distintas de la indicada. La restauración la usa para
    /// esperar a que las demás terminales hayan salido.
    /// </summary>
    Task<int> ContarSesionesVigentesExceptoAsync(int idTerminal, CancellationToken cancellationToken = default);
}
