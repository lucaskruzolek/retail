using Retail.Application.DTOs.Auth;
using Retail.Domain.Enums;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la gestión en memoria de la sesión del operador autenticado (RF-01, RF-02, RNF-06).
/// Expone flags RBAC reactivos y notificaciones de cambio de usuario.
/// </summary>
public interface ICurrentUserSession
{
    LoginResultDto? UsuarioActual { get; }

    bool EstaAutenticado { get; }

    int? IdUsuario { get; }

    string NombreUsuario { get; }

    string NombreCompleto { get; }

    RolUsuarioEnum? Rol { get; }

    bool EsGerente { get; }

    bool EsEncargado { get; }

    bool EsCajero { get; }

    event Action? SessionChanged;

    void EstablecerSesion(LoginResultDto usuario);

    void CerrarSesion();
}
