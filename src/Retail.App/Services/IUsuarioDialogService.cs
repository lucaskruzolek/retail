using Retail.Application.DTOs.Usuarios;

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

    CrearUsuarioDto? MostrarDialogoCrear(Func<CrearUsuarioDto, Task>? onGuardarAsync = null);

    ModificarUsuarioDto? MostrarDialogoModificar(UsuarioDto usuario, Func<ModificarUsuarioDto, Task>? onGuardarAsync = null);

    CambiarPasswordDto? MostrarDialogoCambiarPassword(UsuarioDto usuario, Func<CambiarPasswordDto, Task>? onGuardarAsync = null);
}
