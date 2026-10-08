using System.Windows;
using Retail.App.ViewModels.Articulos;

namespace Retail.App.Views.Dialogs;

public partial class FraccionarDialog : Wpf.Ui.Controls.FluentWindow
{
    public FraccionarViewModel ViewModel { get; }

    public FraccionarDialog() : this(new FraccionarViewModel())
    {
    }

    public FraccionarDialog(FraccionarViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    private async void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Validar())
        {
            return;
        }

        if (ViewModel.OnConfirmarAsync != null)
        {
            try
            {
                ViewModel.IsBusy = true;
                ViewModel.UnidadesObtenidas = await ViewModel.OnConfirmarAsync();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                // Por ejemplo, StockInsuficienteException si el stock del origen cambió desde que se abrió el diálogo.
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
