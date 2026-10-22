using Retail.Application.DTOs.Auth;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para el servicio de autenticación de operadores en el sistema.
/// </summary>
public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reautenticación (<i>step-up</i>): confirma que quien opera la sesión abierta conoce la contraseña del
    /// usuario antes de una operación crítica, como un backup o una restauración (RF-22).
    /// </summary>
    /// <exception cref="Retail.Domain.Exceptions.CredencialesInvalidasException">
    /// La contraseña no coincide o el usuario no está activo.
    /// </exception>
    /// <exception cref="Retail.Domain.Exceptions.CuentaBloqueadaException">
    /// La contraseña es correcta pero la cuenta está bloqueada por intentos de autorización fallidos (D-31).
    /// </exception>
    Task VerificarCredencialAsync(int idUsuario, string password, CancellationToken cancellationToken = default);
}
