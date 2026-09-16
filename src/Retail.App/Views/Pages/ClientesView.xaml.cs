using System.Windows.Controls;
using Retail.App.ViewModels.Clientes;

namespace Retail.App.Views.Pages;

public partial class ClientesView : UserControl
{
    public ClientesViewModel ViewModel { get; }

    public ClientesView(ClientesViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F2)
            {
                binding.Command = ViewModel.NuevoClienteCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F3)
            {
                binding.Command = ViewModel.AlternarSoloConDeudaCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F5)
            {
                binding.Command = ViewModel.CargarClientesCommand;
            }
        }

        Loaded += async (_, _) =>
        {
            if (ViewModel.Clientes.Count == 0)
            {
                await ViewModel.CargarClientesCommand.ExecuteAsync(null);
            }
        };
    }
}
