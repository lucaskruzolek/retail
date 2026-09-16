using System.Windows;
using Retail.App.ViewModels.Clientes;

namespace Retail.App.Views.Dialogs;

public partial class CobranzaModalDialog : Wpf.Ui.Controls.FluentWindow
{
    public CobranzaModalViewModel ViewModel { get; }

    public CobranzaModalDialog(CobranzaModalViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        ViewModel.RequestClose += (sender, result) =>
        {
            DialogResult = result;
            Close();
        };

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }
}
