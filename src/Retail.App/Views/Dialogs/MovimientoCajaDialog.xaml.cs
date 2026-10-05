using System.Windows;
using Retail.App.ViewModels.Caja;
using Retail.Domain.Enums;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

public partial class MovimientoCajaDialog : FluentWindow
{
    public MovimientoCajaViewModel ViewModel { get; }

    public MovimientoCajaDialog(int idTurno)
    {
        InitializeComponent();
        ViewModel = new MovimientoCajaViewModel(idTurno);
        DataContext = ViewModel;

        CbTipoMovimiento.ItemsSource = new[]
        {
            TipoMovimientoCajaEnum.IngresoExtraordinario,
            TipoMovimientoCajaEnum.RetiroExtraordinario
        };
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Monto <= 0)
        {
            System.Windows.MessageBox.Show("El monto ingresado debe ser mayor a cero.", "Validación", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ViewModel.Concepto))
        {
            System.Windows.MessageBox.Show("El concepto del movimiento es obligatorio.", "Validación", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
