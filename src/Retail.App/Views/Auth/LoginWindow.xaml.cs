using System.Windows;
using Retail.App.ViewModels.Auth;

namespace Retail.App.Views.Auth;

public partial class LoginWindow : Wpf.Ui.Controls.FluentWindow
{
    public LoginViewModel ViewModel { get; }

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        ViewModel.LoginExitoso += OnLoginExitoso;

        Loaded += (_, _) => TxtUsuario.Focus();
    }

    private void OnLoginExitoso()
    {
        DialogResult = true;
        Close();
    }

    private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EstaCargando)
        {
            return;
        }

        await ViewModel.IniciarSesionAsync(TxtPassword.Password);
    }
}
