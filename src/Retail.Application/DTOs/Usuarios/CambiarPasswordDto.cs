namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Parámetros para el blanqueo o cambio de contraseña de un usuario.
/// </summary>
public record class CambiarPasswordDto
{
    public required int IdUsuario { get; init; }
    public required string NuevaPassword { get; init; }
}
