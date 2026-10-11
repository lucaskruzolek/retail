using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Parámetros para la modificación de datos de un operador.
/// </summary>
public record class ModificarUsuarioDto
{
    public required int IdUsuario { get; init; }
    public required string Nombre { get; init; }
    public required string Apellido { get; init; }
    public required RolUsuarioEnum Rol { get; init; }
}
