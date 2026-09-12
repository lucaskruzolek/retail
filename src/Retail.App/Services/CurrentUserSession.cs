using Retail.Application.DTOs.Auth;
using Retail.Domain.Enums;

namespace Retail.App.Services;

/// <summary>
/// Implementación Singleton del contexto de sesión en memoria del operador (RF-01, RF-02, RNF-06).
/// Permite conmutar la identidad del operador activo en &lt; 15 ms sin reiniciar la aplicación.
/// </summary>
public class CurrentUserSession : ICurrentUserSession
{
    private LoginResultDto? _usuarioActual;

    public LoginResultDto? UsuarioActual => _usuarioActual;

    public bool EstaAutenticado => _usuarioActual != null;

    public int? IdUsuario => _usuarioActual?.IdUsuario;

    public string NombreUsuario => _usuarioActual?.NombreUsuario ?? string.Empty;

    public string NombreCompleto => _usuarioActual?.NombreCompleto ?? string.Empty;

    public RolUsuarioEnum? Rol => _usuarioActual?.Rol;

    public bool EsGerente => _usuarioActual?.Rol == RolUsuarioEnum.Gerente;

    public bool EsEncargado => _usuarioActual?.Rol == RolUsuarioEnum.Encargado;

    public bool EsCajero => _usuarioActual?.Rol == RolUsuarioEnum.Cajero;

    public event Action? SessionChanged;

    public void EstablecerSesion(LoginResultDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        _usuarioActual = usuario;
        SessionChanged?.Invoke();
    }

    public void CerrarSesion()
    {
        _usuarioActual = null;
        SessionChanged?.Invoke();
    }
}
