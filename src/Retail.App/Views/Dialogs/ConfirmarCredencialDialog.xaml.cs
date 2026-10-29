using System.ComponentModel;
using System.Windows;
using Retail.App.ViewModels.Backups;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Pide la credencial (la contraseña propia o el usuario y la contraseña del Gerente que autoriza) y ejecuta la operación sensible (copia o restauración) con su progreso (D-8).
/// </summary>
public partial class ConfirmarCredencialDialog : Wpf.Ui.Controls.FluentWindow
{
    public ConfirmarCredencialViewModel ViewModel { get; }

    public ConfirmarCredencialDialog(ConfirmarCredencialViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Una copia o una restauración a medias no se interrumpe cerrando la ventana.
        if (ViewModel.IsBusy)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }

    private void TxtPasswordActor_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.PasswordBox pb)
        {
            ViewModel.PasswordActor = pb.Password;
        }
    }

    private async void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        var debeCerrar = await ViewModel.EjecutarAsync();

        if (!debeCerrar)
        {
            // Una contraseña incorrecta se vació en el ViewModel (los campos la reflejan); un dato faltante la conserva.
            TxtPassword.Password = ViewModel.Password;
            TxtPasswordActor.Password = ViewModel.PasswordActor;
            (ViewModel.PideUsuarioAutorizante ? TxtPasswordActor : TxtPassword).Focus();
            return;
        }

        DialogResult = ViewModel.Completada;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
