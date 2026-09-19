using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.ViewModels.Ventas;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Implementación concreta de los diálogos de la terminal de mostrador (POS).
/// </summary>
public class VentaDialogService : IVentaDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public VentaDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<IReadOnlyList<PagoVentaDto>?> MostrarCobroModalAsync(decimal totalVenta, ClienteDto? cliente)
    {
        var viewModel = new CobroModalViewModel(totalVenta, cliente);
        var dialog = new CobroModalDialog(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        bool? resultado = dialog.ShowDialog();
        if (resultado == true && viewModel.OperacionConfirmada)
        {
            return Task.FromResult<IReadOnlyList<PagoVentaDto>?>(viewModel.PagosImputados.ToList());
        }

        return Task.FromResult<IReadOnlyList<PagoVentaDto>?>(null);
    }

    public Task<ClienteDto?> MostrarSeleccionarClienteModalAsync(ClienteDto? clienteActual)
    {
        var clienteService = _serviceProvider.GetRequiredService<IClienteService>();
        var viewModel = new SeleccionarClienteModalViewModel(clienteService);
        var dialog = new SeleccionarClienteModalDialog(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        bool? resultado = dialog.ShowDialog();
        if (resultado == true && viewModel.OperacionConfirmada)
        {
            return Task.FromResult(viewModel.ClienteElegido);
        }

        return Task.FromResult(clienteActual);
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var res = owner != null
            ? MessageBox.Show(owner, mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question);

        return res == MessageBoxResult.Yes;
    }

    public void MostrarAlerta(string titulo, string mensaje)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        if (owner != null)
        {
            MessageBox.Show(owner, mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void MostrarError(string titulo, string mensaje)
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        if (owner != null)
        {
            MessageBox.Show(owner, mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
