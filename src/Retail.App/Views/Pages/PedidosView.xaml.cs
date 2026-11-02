using System.Windows.Controls;
using Retail.App.ViewModels.Pedidos;

namespace Retail.App.Views.Pages;

/// <summary>
/// Bandeja de pedidos a proveedor (IU-12, Track 4.3).
/// </summary>
public partial class PedidosView : UserControl
{
    public PedidosViewModel ViewModel { get; }

    public PedidosView(PedidosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (!ViewModel.FiltroProveedor.TieneOpciones)
            {
                await ViewModel.InicializarAsync();
            }
        };
    }
}
