using System.Windows;
using System.Windows.Input;
using Retail.App.ViewModels.Compras;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Diálogo modal para la selección e importación de un pedido emitido en el módulo de compras (Track 4.3, P2.4).
/// </summary>
public partial class SeleccionarPedidoModalDialog : FluentWindow
{
    public SeleccionarPedidoModalViewModel ViewModel { get; }

    public SeleccionarPedidoModalDialog(SeleccionarPedidoModalViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Pedidos.Count == 0 && !ViewModel.IsBusy)
        {
            _ = ViewModel.CargarPedidosCommand.ExecuteAsync(null);
        }
    }

    private void OnSeleccionarClick(object sender, RoutedEventArgs e)
    {
        ConfirmarSeleccion();
    }

    private void OnDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmarSeleccion();
    }

    private void ConfirmarSeleccion()
    {
        if (ViewModel.PedidoSeleccionado != null)
        {
            ViewModel.SeleccionarPedido(ViewModel.PedidoSeleccionado);
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

