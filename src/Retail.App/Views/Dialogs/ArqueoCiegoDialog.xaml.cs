using System.Windows;
using Retail.App.ViewModels.Caja;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

public partial class ArqueoCiegoDialog : FluentWindow
{
    public ArqueoCiegoDialog(int idTurno)
    {
        InitializeComponent();
        DataContext = new ArqueoCiegoViewModel(idTurno);
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
