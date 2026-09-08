using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Parámetros para la modificación de datos de un operador.
/// </summary>
public record class ModificarUsuarioDto
{
    public required int IdUsuario { get; init; }
    public required string NombreCompleto { get; init; }
    public required RolUsuarioEnum Rol { get; init; }
}
