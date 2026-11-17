using System.Windows;
using Retail.App.ViewModels.Presupuestos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Detalle de solo lectura de un presupuesto (IU-13): cabecera, renglones y totales, con imprimir y exportar a PDF.
/// </summary>
public partial class DetallePresupuestoDialog : FluentWindow
{
    public DetallePresupuestoViewModel ViewModel { get; }

    public DetallePresupuestoDialog(DetallePresupuestoViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (!ViewModel.Cargado && !ViewModel.IsBusy)
            {
                await ViewModel.InicializarAsync();
            }
        };
    }

    private void OnCerrarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
