using System.Windows;
using Retail.App.ViewModels.Articulos;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Code-behind del diálogo modal de administración de rubros, categorías y marcas.
/// El ciclo de vida del ViewModel lo gobierna quien abre el diálogo (IClasificacionDialogService).
/// </summary>
public partial class AdministrarClasificacionDialog : FluentWindow
{
    public AdministrarClasificacionDialog()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdministrarClasificacionViewModel viewModel)
        {
            await viewModel.InicializarAsync();
        }
    }
}
