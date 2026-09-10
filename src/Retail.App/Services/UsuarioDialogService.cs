using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.ViewModels.Usuarios;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Usuarios;

namespace Retail.App.Services;

/// <summary>
/// Implementación de los servicios de diálogo para el módulo de usuarios utilizando ventanas Fluent modales.
/// </summary>
public class UsuarioDialogService : IUsuarioDialogService
{
    private readonly ILogger<UsuarioDialogService> _logger;

    public UsuarioDialogService(ILogger<UsuarioDialogService>? logger = null)
    {
        _logger = logger ?? NullLogger<UsuarioDialogService>.Instance;
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        var result = MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    public void MostrarError(string titulo, string mensaje)
    {
        _logger.LogError("Diálogo de error presentado en mostrador. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);

        MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public void MostrarInformacion(string titulo, string mensaje)
    {
        MessageBox.Show(
            mensaje,
            titulo,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public CrearUsuarioDto? MostrarDialogoCrear(Func<CrearUsuarioDto, Task>? onGuardarAsync = null)
    {
        var vm = new UsuarioFormViewModel();
        vm.ConfigurarAlta();

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.ObtenerCrearDto());
            };
        }

        var dialog = new UsuarioFormDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true ? vm.ObtenerCrearDto() : null;
    }

    public ModificarUsuarioDto? MostrarDialogoModificar(UsuarioDto usuario, Func<ModificarUsuarioDto, Task>? onGuardarAsync = null)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var vm = new UsuarioFormViewModel();
        vm.ConfigurarEdicion(usuario);

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.ObtenerModificarDto());
            };
        }

        var dialog = new UsuarioFormDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true ? vm.ObtenerModificarDto() : null;
    }

    public CambiarPasswordDto? MostrarDialogoCambiarPassword(UsuarioDto usuario, Func<CambiarPasswordDto, Task>? onGuardarAsync = null)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var vm = new CambiarPasswordViewModel();
        vm.Configurar(usuario);

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.ObtenerDto());
            };
        }

        var dialog = new CambiarPasswordDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();

        return result == true ? vm.ObtenerDto() : null;
    }
}
