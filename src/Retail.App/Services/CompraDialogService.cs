using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Retail.App.ViewModels.Compras;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Interfaces.Services;

namespace Retail.App.Services;

/// <summary>
/// Implementación concreta de los diálogos del módulo de compras (Track 4.3).
/// </summary>
public class CompraDialogService : ICompraDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public CompraDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

    public Task<PedidoEmitidoDto?> MostrarSeleccionarPedidoModalAsync(int? idProveedor, string? razonSocialProveedor)
    {
        var pedidoService = _serviceProvider.GetRequiredService<IPedidoService>();
        var logger = _serviceProvider.GetService<ILogger<SeleccionarPedidoModalViewModel>>();

        using var viewModel = new SeleccionarPedidoModalViewModel(idProveedor, razonSocialProveedor, pedidoService, logger);
        var dialog = new SeleccionarPedidoModalDialog(viewModel)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        bool? resultado = dialog.ShowDialog();
        if (resultado == true && viewModel.OperacionConfirmada)
        {
            return Task.FromResult(viewModel.PedidoElegido);
        }

        return Task.FromResult<PedidoEmitidoDto?>(null);
    }
}

