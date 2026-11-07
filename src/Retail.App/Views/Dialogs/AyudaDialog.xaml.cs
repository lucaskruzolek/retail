using System.Windows;
using Retail.App.ViewModels.Ayuda;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Diálogo modal con la guía de atajos de mostrador y acceso al manual de usuario en PDF.
/// </summary>
public partial class AyudaDialog : Wpf.Ui.Controls.FluentWindow
{
    public AyudaViewModel ViewModel { get; }

    public AyudaDialog(AyudaViewModel viewModel)
    {
        InitializeComponent();

        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        ViewModel.SolicitarCerrar += Close;

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }
}

