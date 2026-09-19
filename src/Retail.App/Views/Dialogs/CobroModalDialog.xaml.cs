using System.Windows;
using Retail.App.ViewModels.Ventas;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Ventana modal de checkout multimedio y cobro de venta (RF-09, RF-20).
/// </summary>
public partial class CobroModalDialog : FluentWindow
{
    public CobroModalViewModel ViewModel { get; }

    public CobroModalDialog(CobroModalViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();
    }

    private void OnConfirmarClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ConfirmarCobro();
        if (ViewModel.OperacionConfirmada)
        {
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
