namespace Retail.Application.DTOs.Usuarios;

/// <summary>
/// Parámetros para el cambio de contraseña de un usuario. Si quien lo ejecuta es el titular, es un cambio propio;
/// si es otro usuario (Gerente o Administrador), es un blanqueo y el titular deberá cambiarla al ingresar.
/// </summary>
public record class CambiarPasswordDto
{
    public required int IdUsuario { get; init; }
    public required string NuevaPassword { get; init; }

    /// <summary>
    /// Obligatoria cuando el titular cambia su propia contraseña: impide que alguien tome la cuenta desde una
    /// sesión abierta. En un blanqueo (otro usuario) no se usa.
    /// </summary>
    public string? PasswordActual { get; init; }
}
