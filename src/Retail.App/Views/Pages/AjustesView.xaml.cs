using System.Windows.Controls;
using Retail.App.ViewModels.Ajustes;

namespace Retail.App.Views.Pages;

public partial class AjustesView : UserControl
{
    public AjustesViewModel ViewModel { get; }

    public AjustesView(AjustesViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        // La política de redondeo se lee al abrir la página (solo carga si el usuario es Gerente).
        Loaded += async (_, _) => await ViewModel.CargarCommand.ExecuteAsync(null);
    }
}
