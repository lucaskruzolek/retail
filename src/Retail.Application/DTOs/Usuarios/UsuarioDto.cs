using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Información pública de un usuario del sistema.
/// </summary>
public record class UsuarioDto
{
    public required int IdUsuario { get; init; }
    public required string NombreUsuario { get; init; }
    public required string NombreCompleto { get; init; }
    public required RolUsuarioEnum Rol { get; init; }
    public required bool Activo { get; init; }
    public required DateTime CreatedAt { get; init; }
}
