using System.Windows;
using Retail.App.ViewModels.Proveedores;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Code-behind del diálogo modal de equivalencias de clasificación de un proveedor (Track 6.6, Fase 6).
/// El ciclo de vida del ViewModel lo gobierna quien abre el diálogo (IProveedorDialogService).
/// </summary>
public partial class EquivalenciasProveedorDialog : FluentWindow
{
    public EquivalenciasProveedorDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EquivalenciasProveedorViewModel viewModel)
        {
            await viewModel.InicializarAsync();
        }
    }
}
