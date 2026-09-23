using System.Windows.Controls;
using Retail.App.ViewModels.Compras;

namespace Retail.App.Views.Pages;

/// <summary>
/// Lógica de interacción para ComprasView.xaml
/// </summary>
public partial class ComprasView : UserControl
{
    public ComprasViewModel ViewModel { get; }

    public ComprasView(ComprasViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F2)
            {
                binding.Command = ViewModel.AgregarItemCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F12)
            {
                binding.Command = ViewModel.RegistrarCompraCommand;
            }
        }

        Loaded += async (_, _) =>
        {
            if (ViewModel.Proveedores.Count == 0)
            {
                await ViewModel.CargarProveedoresCommand.ExecuteAsync(null);
            }
        };
    }
}
