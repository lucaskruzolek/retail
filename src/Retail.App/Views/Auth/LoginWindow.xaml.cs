using System.Windows;
using System.Windows.Input;
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
        await ViewModel.IniciarSesionAsync(TxtPassword.Password);
    }

    private async void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.IniciarSesionAsync(TxtPassword.Password);
        }
    }
}
