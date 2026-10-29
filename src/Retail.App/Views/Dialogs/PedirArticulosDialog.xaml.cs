using System.Windows;
using Retail.App.ViewModels.Pedidos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Modal de pedido (A-17): el usuario revisa el lote agrupado por proveedor, ajusta cantidades y confirma, o elige
/// incorporar primero los ítems sin incorporar. El guardado lo hace quien abre el modal con
/// <see cref="PedirArticulosViewModel.ObtenerRespuesta"/>.
/// </summary>
public partial class PedirArticulosDialog : FluentWindow
{
    public PedirArticulosViewModel ViewModel { get; }

    public PedirArticulosDialog(PedirArticulosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();
    }

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeConfirmar)
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnIncorporarPrimero_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.PuedeIncorporarPrimero)
        {
            return;
        }

        ViewModel.SolicitarIncorporacion();
        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
