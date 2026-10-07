using System.Windows;
using Retail.App.ViewModels.Caja;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

public partial class AperturaTurnoDialog : FluentWindow
{
    public AperturaTurnoViewModel ViewModel { get; }

    public AperturaTurnoDialog()
    {
        InitializeComponent();
        ViewModel = new AperturaTurnoViewModel();
        DataContext = ViewModel;
    }

    private void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
