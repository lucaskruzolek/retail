using System.Windows.Controls;
using Retail.App.ViewModels.Backups;

namespace Retail.App.Views.Pages;

public partial class BackupView : UserControl
{
    public BackupViewModel ViewModel { get; }

    public BackupView(BackupViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            if (ViewModel.Copias.Count == 0)
            {
                await ViewModel.CargarCommand.ExecuteAsync(null);
            }
        };
    }
}
