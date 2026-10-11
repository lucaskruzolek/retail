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
}
