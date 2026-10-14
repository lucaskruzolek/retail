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

    /// <summary>
    /// Obligatorio para el rol Gerente. Es <c>required</c> aunque admita <c>null</c>: quien construye el DTO
    /// debe decidir el valor, porque omitirlo en una edición borraría el email guardado.
    /// </summary>
    public required string? EmailRecuperacion { get; init; }
}
