using System.Windows.Controls;
using Retail.App.ViewModels.Caja;

namespace Retail.App.Views.Pages;

public partial class CajaView : UserControl
{
    public CajaViewModel ViewModel { get; }

    public CajaView(CajaViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;
        InitializeComponent();
    }
}
