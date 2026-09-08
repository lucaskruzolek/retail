using Retail.Application.DTOs.Auth;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para el servicio de autenticación de operadores en el sistema.
/// </summary>
public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
