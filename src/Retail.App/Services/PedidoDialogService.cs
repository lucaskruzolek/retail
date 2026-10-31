using System.Windows;
using Microsoft.Extensions.Logging;
using Retail.App.ViewModels.Pedidos;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Pedidos;

namespace Retail.App.Services;

/// <summary>
/// Implementación WPF de los diálogos del módulo de pedidos a proveedor (Track 4.3).
/// </summary>
public class PedidoDialogService : IPedidoDialogService
{
    private readonly ILogger<PedidoDialogService> _logger;

    public PedidoDialogService(ILogger<PedidoDialogService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool Confirmar(string titulo, string mensaje)
    {
        return MessageBox.Show(mensaje, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void MostrarAlerta(string titulo, string mensaje)
    {
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void MostrarError(string titulo, string mensaje)
    {
        _logger.LogError("Diálogo de error en pedidos. Título: {Titulo}, Mensaje: {Mensaje}", titulo, mensaje);
        MessageBox.Show(mensaje, titulo, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public RespuestaPedirArticulos MostrarPedirArticulos(
        VistaPreviaPedidoDto vistaPrevia,
        IReadOnlyList<string> avisosPrevios,
        bool ofrecerIncorporar)
    {
        ArgumentNullException.ThrowIfNull(vistaPrevia);
        ArgumentNullException.ThrowIfNull(avisosPrevios);

        var viewModel = new PedirArticulosViewModel(vistaPrevia, avisosPrevios, ofrecerIncorporar);
        var dialog = new PedirArticulosDialog(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? viewModel.ObtenerRespuesta() : RespuestaPedirArticulos.Cancelada;
    }
}
