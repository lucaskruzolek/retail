using System.Windows;
using System.Windows.Input;
using Retail.App.ViewModels.Usuarios;

namespace Retail.App.Views.Dialogs;

public partial class UsuarioFormDialog : Wpf.Ui.Controls.FluentWindow
{
    public UsuarioFormViewModel ViewModel { get; }

    public UsuarioFormDialog(UsuarioFormViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        DataObject.AddPastingHandler(TxtNombreUsuario, OnPastingNombreUsuario);

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    private void TxtPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.PasswordBox pb)
        {
            ViewModel.Password = pb.Password;
        }
    }

    private void TxtNombreUsuario_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void TxtNombreUsuario_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Contains(' ', StringComparison.Ordinal))
        {
            e.Handled = true;
        }
    }

    private void OnPastingNombreUsuario(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            var text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
            if (!string.IsNullOrEmpty(text) && text.Contains(' ', StringComparison.Ordinal))
            {
                var cleaned = text.Replace(" ", string.Empty, StringComparison.Ordinal);
                var dataObj = new DataObject();
                dataObj.SetText(cleaned);
                e.DataObject = dataObj;
            }
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
