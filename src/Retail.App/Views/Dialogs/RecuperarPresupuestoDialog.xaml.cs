using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Retail.App.ViewModels.Presupuestos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// "Recuperar presupuesto" del Punto de venta (RF-12): pide el número y se cierra solo cuando lo recuperó; si falla, muestra
/// el motivo dentro del diálogo.
/// </summary>
public partial class RecuperarPresupuestoDialog : FluentWindow
{
    public RecuperarPresupuestoViewModel ViewModel { get; }

    public RecuperarPresupuestoDialog(RecuperarPresupuestoViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Loaded += (_, _) => TxtNumero.Focus();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RecuperarPresupuestoViewModel.Recuperado) && ViewModel.Recuperado)
        {
            Close();
        }
    }

    /// <summary>El número admite solo dígitos: se descarta cualquier otro carácter tipeado.</summary>
    private void OnNumeroPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsAsciiDigit);
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
