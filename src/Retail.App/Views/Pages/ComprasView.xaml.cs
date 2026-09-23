using System.Windows.Controls;
using Retail.App.ViewModels.Compras;

namespace Retail.App.Views.Pages;

/// <summary>
/// Lógica de interacción para ComprasView.xaml
/// </summary>
public partial class ComprasView : UserControl
{
    public ComprasView(ComprasViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }
}
