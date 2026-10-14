using Retail.Application.DTOs.Usuarios;
using Retail.Domain.Enums;

namespace Retail.App.Services;

/// <summary>
/// Contrato para la interacción con diálogos y ventanas modales del módulo de usuarios.
/// Desacopla la lógica de presentación (ViewModels) de la instanciación de ventanas WPF para facilitar pruebas unitarias.
/// </summary>
public interface IUsuarioDialogService
{
    bool Confirmar(string titulo, string mensaje);

    void MostrarError(string titulo, string mensaje);

    void MostrarInformacion(string titulo, string mensaje);

    CrearUsuarioDto? MostrarDialogoCrear(
        IReadOnlyList<RolUsuarioEnum> rolesAsignables,
        Func<CrearUsuarioDto, Task>? onGuardarAsync = null);

    ModificarUsuarioDto? MostrarDialogoModificar(
        UsuarioDto usuario,
        IReadOnlyList<RolUsuarioEnum> rolesAsignables,
        Func<ModificarUsuarioDto, Task>? onGuardarAsync = null);

    CambiarPasswordDto? MostrarDialogoCambiarPassword(UsuarioDto usuario, Func<CambiarPasswordDto, Task>? onGuardarAsync = null);

    /// <summary>
    /// Cambio voluntario de la propia contraseña, que pide la contraseña actual. Devuelve <c>true</c> si se cambió.
    /// </summary>
    bool MostrarDialogoCambioPropio(UsuarioDto usuario, Func<CambiarPasswordDto, Task> onGuardarAsync);

    /// <summary>
    /// Cambio de contraseña obligatorio al iniciar sesión (D-6). Devuelve <c>true</c> si el titular la cambió.
    /// </summary>
    bool MostrarDialogoCambioObligatorio(
        int idUsuario,
        string nombreUsuario,
        string nombreCompleto,
        Func<CambiarPasswordDto, Task> onGuardarAsync);
}
