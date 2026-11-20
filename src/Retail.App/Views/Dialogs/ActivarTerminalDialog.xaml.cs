using System.Windows;
using Retail.App.ViewModels.Terminales;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// "Activar esta PC" (MT-1): un Gerente convierte esta PC en una terminal nueva o toma el lugar de una existente. El
/// diálogo se cierra con resultado afirmativo cuando el ViewModel terminó de activar; los errores se muestran adentro.
/// </summary>
public partial class ActivarTerminalDialog : FluentWindow
{
    public ActivarTerminalViewModel ViewModel { get; }

    public ActivarTerminalDialog(ActivarTerminalViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        ViewModel.RequestClose += (_, resultado) => DialogResult = resultado;

        // Las terminales existentes se leen al abrir, para ofrecerlas en "Reemplazar una existente".
        Loaded += async (_, _) =>
        {
            TxtNombre.Focus();
            await ViewModel.CargarAsync();
        };
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
