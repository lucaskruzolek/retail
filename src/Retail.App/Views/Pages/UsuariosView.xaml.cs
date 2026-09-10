using System.Windows.Controls;
using Retail.App.ViewModels.Usuarios;

namespace Retail.App.Views.Pages;

public partial class UsuariosView : UserControl
{
    public UsuariosViewModel ViewModel { get; }

    public UsuariosView(UsuariosViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            if (ViewModel.Usuarios.Count == 0)
            {
                await ViewModel.CargarUsuariosCommand.ExecuteAsync(null);
            }
        };
    }
}
