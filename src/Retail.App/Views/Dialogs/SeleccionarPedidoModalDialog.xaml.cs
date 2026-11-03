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
    /// <summary>Ancho de la ventana cuando se lista también el proveedor de cada pedido.</summary>
    public const double AnchoConProveedor = 760;

    public SeleccionarPedidoModalViewModel ViewModel { get; }

    public SeleccionarPedidoModalDialog(SeleccionarPedidoModalViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        // Las columnas de un DataGrid no heredan el DataContext, así que no pueden enlazar su Visibility. El alcance (un
        // proveedor o todos) se fija al abrir el diálogo: con varios proveedores se agrega la columna y se ensancha la ventana.
        if (ViewModel.MostrarProveedor)
        {
            ColumnaProveedor.Visibility = Visibility.Visible;
            Width = AnchoConProveedor;
        }
        else
        {
            ColumnaProveedor.Visibility = Visibility.Collapsed;
        }

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

