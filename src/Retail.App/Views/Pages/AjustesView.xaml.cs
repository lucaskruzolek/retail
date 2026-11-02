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
    }
}
