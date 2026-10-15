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

    /// <summary>
    /// La contraseña vigente la eligió otra persona: la UI debe exigir el cambio antes de abrir el sistema.
    /// </summary>
    public bool DebeCambiarPassword { get; init; }
}
