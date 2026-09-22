using System.Windows;
using System.Windows.Input;
using Retail.App.ViewModels.Articulos;

namespace Retail.App.Views.Dialogs;

public partial class SeleccionarCatalogoProveedorModalDialog
{
    public SeleccionarCatalogoProveedorModalDialog()
    {
        InitializeComponent();
    }

    public SeleccionarCatalogoProveedorModalDialog(SeleccionarCatalogoProveedorModalViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SeleccionarCatalogoProveedorModalViewModel vm && vm.ItemSeleccionado != null)
        {
            vm.SeleccionarCommand.Execute(this);
        }
    }

    private void OnSeleccionarClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SeleccionarCatalogoProveedorModalViewModel vm)
        {
            vm.SeleccionarCommand.Execute(this);
        }
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SeleccionarCatalogoProveedorModalViewModel vm)
        {
            vm.CancelarCommand.Execute(this);
        }
        else
        {
            DialogResult = false;
            Close();
        }
    }
}
