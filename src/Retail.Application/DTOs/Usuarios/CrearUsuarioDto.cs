using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Parámetros de entrada para el alta de un nuevo operador en el sistema.
/// </summary>
public record class CrearUsuarioDto
{
    public required string NombreUsuario { get; init; }
    public required string Password { get; init; }
    public required string NombreCompleto { get; init; }
    public required RolUsuarioEnum Rol { get; init; }
}
