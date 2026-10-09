using Retail.Application.DTOs.Common;
using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Información pública de un usuario del sistema.
/// </summary>
public record class UsuarioDto : BaseDto
{
    public required int IdUsuario { get; init; }
    public required string NombreUsuario { get; init; }
    public required string Nombre { get; init; }
    public required string Apellido { get; init; }
    public string NombreCompleto => $"{Nombre} {Apellido}";
    public required RolUsuarioEnum Rol { get; init; }
    public required bool Activo { get; init; }
    public required DateTime CreatedAt { get; init; }
}
