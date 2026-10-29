using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Articulos;

namespace Retail.App.Views.Dialogs;

public partial class SeleccionarCatalogoProveedorModalDialog
{
    public SeleccionarCatalogoProveedorModalDialog()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    public SeleccionarCatalogoProveedorModalDialog(SeleccionarCatalogoProveedorModalViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// El doble clic solo elige filas habilitadas: una fila de otro artículo (A-1) no dispara la selección.
    /// </summary>
    private void OnDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SeleccionarCatalogoProveedorModalViewModel vm
            && e.OriginalSource is DependencyObject origen
            && ItemsControl.ContainerFromElement((DataGrid)sender, origen) is DataGridRow { IsEnabled: true, Item: ItemCatalogoSelectorFila { EsSeleccionable: true } })
        {
            vm.SeleccionarCommand.Execute(this);
        }
    }
}
