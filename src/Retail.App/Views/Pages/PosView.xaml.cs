using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Ventas;

namespace Retail.App.Views.Pages;

/// <summary>
/// Terminal de Punto de Venta (POS) y Mostrador (RF-09, RF-10, RNF-01).
/// </summary>
public partial class PosView : UserControl
{
    public PosViewModel ViewModel { get; }

    public PosView(PosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        InitializeComponent();

        Loaded += (_, _) =>
        {
            TxtBuscador.Focus();
        };
    }

    private void OnTxtBuscadorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ProcesarEnterCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ViewModel.MostrarPopupBusqueda)
        {
            ViewModel.MoverSeleccionPopupAbajoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ViewModel.MostrarPopupBusqueda)
        {
            ViewModel.MoverSeleccionPopupArribaCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && ViewModel.MostrarPopupBusqueda)
        {
            ViewModel.CerrarPopupBusquedaCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnF1Click(object sender, RoutedEventArgs e)
    {
        TxtBuscador.Focus();
        TxtBuscador.SelectAll();
    }
}
