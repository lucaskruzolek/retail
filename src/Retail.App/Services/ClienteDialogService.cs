using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.ViewModels.Clientes;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Implementación de los servicios de diálogo modal para el módulo de clientes y cuentas corrientes.
/// </summary>
public class ClienteDialogService : IClienteDialogService
{
    private readonly ILogger<ClienteDialogService> _logger;
    private readonly IServiceProvider? _serviceProvider;

    public ClienteDialogService(
        IServiceProvider? serviceProvider = null,
        ILogger<ClienteDialogService>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger ?? NullLogger<ClienteDialogService>.Instance;
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
        _logger.LogError("Diálogo de error en clientes. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);

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

    public CrearClienteDto? MostrarDialogoCrear(Func<CrearClienteDto, Task>? onGuardarAsync = null)
    {
        var vm = new ClienteFormViewModel();
        vm.ConfigurarAlta();

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.GenerarCrearDto());
            };
        }

        var dialog = new ClienteFormDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true ? vm.GenerarCrearDto() : null;
    }

    public ActualizarClienteDto? MostrarDialogoModificar(ClienteDto cliente, Func<ActualizarClienteDto, Task>? onGuardarAsync = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        var vm = new ClienteFormViewModel();
        vm.ConfigurarEdicion(cliente);

        if (onGuardarAsync != null)
        {
            vm.OnGuardarAsync = async () =>
            {
                await onGuardarAsync(vm.GenerarActualizarDto());
            };
        }

        var dialog = new ClienteFormDialog(vm);
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return result == true ? vm.GenerarActualizarDto() : null;
    }

    public Task<CobranzaResultadoDto?> MostrarCobranzaModalAsync(ClienteDto cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("No se ha configurado el proveedor de servicios en ClienteDialogService.");
        }

        var clienteService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IClienteService>(_serviceProvider);
        var cajaService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ICajaService>(_serviceProvider);

        var vm = new CobranzaModalViewModel(clienteService, cajaService, cliente);
        var dialog = new CobranzaModalDialog(vm);

        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } owner)
        {
            dialog.Owner = owner;
        }

        var result = dialog.ShowDialog();
        return Task.FromResult(result == true ? vm.Resultado : null);
    }
}
