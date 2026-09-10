using System.Windows.Controls;
using Retail.App.ViewModels.Articulos;

namespace Retail.App.Views.Pages;

public partial class ArticulosView : UserControl
{
    public ArticulosViewModel ViewModel { get; }

    public ArticulosView(ArticulosViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        // Asegurar vinculación determinística de comandos de mostrador en InputBindings
        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F2)
            {
                binding.Command = ViewModel.NuevoArticuloCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F3)
            {
                binding.Command = ViewModel.AlternarSoloStockCriticoCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F5)
            {
                binding.Command = ViewModel.CargarArticulosCommand;
            }
        }

        Loaded += async (_, _) =>
        {
            if (ViewModel.Articulos.Count == 0)
            {
                await ViewModel.CargarArticulosCommand.ExecuteAsync(null);
            }
        };
    }
}
