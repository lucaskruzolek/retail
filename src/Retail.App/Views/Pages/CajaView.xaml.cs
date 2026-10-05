using System.Windows.Controls;
using Retail.App.ViewModels.Caja;

namespace Retail.App.Views;

public partial class CajaView : UserControl
{
    public CajaView(CajaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}