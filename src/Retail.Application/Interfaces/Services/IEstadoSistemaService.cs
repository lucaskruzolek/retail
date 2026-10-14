using Retail.Application.DTOs.Sistema;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Verificación de solo lectura que la aplicación ejecuta al arrancar en lugar de migrar la base (RF-23).
/// </summary>
public interface IEstadoSistemaService
{
    Task<EstadoSistemaDto> ObtenerEstadoAsync(CancellationToken cancellationToken = default);
}
