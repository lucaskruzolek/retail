using System.Windows;
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

        // El rol no cambia mientras la vista existe: sin permiso para pedir (A-14), la columna de selección sobra.
        ColumnaSeleccion.Visibility = ViewModel.PuedePedir ? Visibility.Visible : Visibility.Collapsed;

        // Asegurar vinculación determinística de comandos de mostrador en InputBindings
        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F2)
            {
                binding.Command = ViewModel.NuevoArticuloCommand;
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
