namespace Retail.Application.DTOs.Auth;

/// <summary>
/// Datos requeridos para la autenticación de un usuario en el sistema.
/// </summary>
public record class LoginRequestDto
{
    public required string NombreUsuario { get; init; }
    public required string Password { get; init; }
}
