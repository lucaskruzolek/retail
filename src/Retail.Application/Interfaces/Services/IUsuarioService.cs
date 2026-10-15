using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la administración de usuarios, roles RBAC y credenciales (RF-03).
/// Cada operación recibe el identificador del usuario que la ejecuta (<c>idActor</c>): el servicio lee su rol de la
/// base y aplica la matriz de autoridad, sin depender de lo que la UI decida mostrar (RNF-06).
/// </summary>
public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarUsuariosAsync(int idActor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Roles que el actor puede asignar o gestionar, para que la UI no ofrezca opciones que el servicio rechazaría.
    /// Devuelve una lista vacía si el actor no gestiona cuentas.
    /// </summary>
    Task<IReadOnlyList<RolUsuarioEnum>> ObtenerRolesAsignablesAsync(int idActor, CancellationToken cancellationToken = default);

    Task<UsuarioDto> ObtenerPorIdAsync(int idActor, int idUsuario, CancellationToken cancellationToken = default);

    Task<UsuarioDto> RegistrarUsuarioAsync(int idActor, CrearUsuarioDto dto, CancellationToken cancellationToken = default);

    Task<UsuarioDto> ModificarUsuarioAsync(int idActor, ModificarUsuarioDto dto, CancellationToken cancellationToken = default);

    Task BajaUsuarioAsync(int idActor, int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si el actor es el titular de la cuenta, cambia su propia contraseña (no requiere rol de gestión).
    /// Si es otro usuario, blanquea la contraseña y el titular deberá cambiarla en su próximo inicio de sesión.
    /// </summary>
    Task CambiarPasswordAsync(int idActor, CambiarPasswordDto dto, CancellationToken cancellationToken = default);
}
