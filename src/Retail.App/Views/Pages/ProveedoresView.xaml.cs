using System.Windows.Controls;
using Retail.App.ViewModels.Proveedores;

namespace Retail.App.Views.Pages;

/// <summary>
/// Code-behind de la vista de gestión de Proveedores y Distribuidores (Etapa 2.2).
/// Recibe ProveedoresViewModel por inyección de dependencias, enlaza atajos de teclado e inicia la carga asíncrona.
/// </summary>
public partial class ProveedoresView : UserControl
{
    public ProveedoresViewModel ViewModel { get; }

    public ProveedoresView(ProveedoresViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        foreach (var binding in InputBindings.OfType<System.Windows.Input.KeyBinding>())
        {
            if (binding.Key == System.Windows.Input.Key.F2)
            {
                binding.Command = ViewModel.NuevoProveedorCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F4)
            {
                binding.Command = ViewModel.EditarProveedorCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F5)
            {
                binding.Command = ViewModel.CargarProveedoresCommand;
            }
            else if (binding.Key == System.Windows.Input.Key.F6)
            {
                binding.Command = ViewModel.AbrirImportadorCommand;
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
