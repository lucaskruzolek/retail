using System.Windows;
using Retail.App.ViewModels.Articulos;

namespace Retail.App.Views.Dialogs;

public partial class PresentacionFormDialog : Wpf.Ui.Controls.FluentWindow
{
    public PresentacionFormViewModel ViewModel { get; }

    public PresentacionFormDialog() : this(new PresentacionFormViewModel())
    {
    }

    public PresentacionFormDialog(PresentacionFormViewModel viewModel)
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
                // Las reglas del Dominio (por ejemplo, un origen que ya es presentación) se muestran en el diálogo.
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
