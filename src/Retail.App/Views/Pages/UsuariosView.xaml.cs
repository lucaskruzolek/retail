using System.Windows;
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

        // "Conectado en" es información de administración: solo la ve el Gerente.
        ColumnaConectadoEn.Visibility = ViewModel.PuedeVerSesiones ? Visibility.Visible : Visibility.Collapsed;

        Loaded += async (_, _) =>
        {
            if (ViewModel.Usuarios.Count == 0)
            {
                await ViewModel.CargarUsuariosCommand.ExecuteAsync(null);
            }
        };
    }
}
