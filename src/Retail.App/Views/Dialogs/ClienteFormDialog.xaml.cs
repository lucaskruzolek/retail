using System.Windows;
using Retail.App.ViewModels.Clientes;

namespace Retail.App.Views.Dialogs;

public partial class ClienteFormDialog : Wpf.Ui.Controls.FluentWindow
{
    public ClienteFormViewModel ViewModel { get; }

    public ClienteFormDialog(ClienteFormViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
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
