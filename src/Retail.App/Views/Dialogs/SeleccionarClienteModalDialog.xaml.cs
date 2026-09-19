using System.Windows;
using Retail.App.ViewModels.Ventas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Diálogo modal de selección rápida de cliente para el Punto de Venta (F4).
/// </summary>
public partial class SeleccionarClienteModalDialog : FluentWindow
{
    public SeleccionarClienteModalViewModel ViewModel { get; }

    public SeleccionarClienteModalDialog(SeleccionarClienteModalViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();
    }

    private void OnSeleccionarClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ClienteSeleccionado != null)
        {
            ViewModel.SeleccionarCliente(ViewModel.ClienteSeleccionado);
            DialogResult = true;
            Close();
        }
    }

    private void OnConsumidorFinalClick(object sender, RoutedEventArgs e)
    {
        ViewModel.EstablecerConsumidorFinal();
        DialogResult = true;
        Close();
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
