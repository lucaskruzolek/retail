using System.Windows;
using Retail.App.ViewModels.Presupuestos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Alerta previa a cargar un presupuesto en el POS (RF-12): precios que cambiaron (si venció), faltantes de stock y artículos
/// dados de baja. Solo informa y pide confirmar: Continuar o Cancelar, sin elegir renglón por renglón (PS-03).
/// </summary>
public partial class AlertaPreciosPresupuestoDialog : FluentWindow
{
    public AlertaPreciosPresupuestoViewModel ViewModel { get; }

    public AlertaPreciosPresupuestoDialog(AlertaPreciosPresupuestoViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();
    }

    private void OnContinuarClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Confirmado = true;
        Close();
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
