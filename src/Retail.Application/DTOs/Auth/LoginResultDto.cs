using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Auth;

/// <summary>
/// Resultado emitido tras una autenticación exitosa de usuario.
/// </summary>
public record class LoginResultDto
{
    public required int IdUsuario { get; init; }
    public required string NombreUsuario { get; init; }
    public required string NombreCompleto { get; init; }
    public required RolUsuarioEnum Rol { get; init; }
}
