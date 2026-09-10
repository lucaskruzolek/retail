using System.Windows;
using Retail.App.ViewModels.Usuarios;

namespace Retail.App.Views.Dialogs;

public partial class CambiarPasswordDialog : Wpf.Ui.Controls.FluentWindow
{
    public CambiarPasswordViewModel ViewModel { get; }

    public CambiarPasswordDialog(CambiarPasswordViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    private void TxtNuevaPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.PasswordBox pb)
        {
            ViewModel.NuevaPassword = pb.Password;
        }
    }

    private void TxtConfirmarPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.PasswordBox pb)
        {
            ViewModel.ConfirmarPassword = pb.Password;
        }
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Validar())
        {
            return;
        }

        if (ViewModel.OnGuardarAsync != null)
        {
            try
            {
                ViewModel.IsBusy = true;
                ViewModel.MensajeError = null;
                await ViewModel.OnGuardarAsync();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ViewModel.MensajeError = ex.Message;
            }
            finally
            {
                ViewModel.IsBusy = false;
            }
        }
        else
        {
            DialogResult = true;
            Close();
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
