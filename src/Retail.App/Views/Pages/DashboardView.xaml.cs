using System.Windows.Controls;
using Retail.App.ViewModels.Reportes;

namespace Retail.App.Views.Pages;

public partial class DashboardView : UserControl
{
    public DashboardViewModel ViewModel { get; }

    public DashboardView(DashboardViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();

        Loaded += async (_, _) => await ViewModel.CargarCommand.ExecuteAsync(null);
    }
}
