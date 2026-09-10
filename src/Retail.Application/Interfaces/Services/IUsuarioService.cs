using Retail.Application.DTOs.Usuarios;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Contrato para la administración de usuarios, roles RBAC y credenciales.
/// </summary>
public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioDto>> ListarUsuariosAsync(CancellationToken cancellationToken = default);

    Task<UsuarioDto> ObtenerPorIdAsync(int idUsuario, CancellationToken cancellationToken = default);

    Task<UsuarioDto> RegistrarUsuarioAsync(CrearUsuarioDto dto, CancellationToken cancellationToken = default);

    Task<UsuarioDto> ModificarUsuarioAsync(ModificarUsuarioDto dto, CancellationToken cancellationToken = default);

    Task BajaUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    Task CambiarPasswordAsync(CambiarPasswordDto dto, CancellationToken cancellationToken = default);
}
