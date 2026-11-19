using System.Windows;
using Retail.App.ViewModels.Terminales;
using Retail.App.Views.Dialogs;

namespace Retail.App.Services;

/// <summary>
/// Implementación WPF de los cuadros del arranque: <see cref="MessageBox"/> para los avisos, con el mismo estilo que el
/// aviso del estado de la base, y el diálogo "Activar esta PC". Todavía no hay ventana principal, así que ninguno tiene dueño.
/// </summary>
public sealed class DialogosArranqueTerminal : IDialogosArranqueTerminal
{
    public void Informar(string mensaje)
    {
        MessageBox.Show(mensaje, "Retail POS - Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void Bloquear(string mensaje)
    {
        MessageBox.Show(mensaje, "Retail POS - Terminal no activada", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public bool ActivarTerminal(ActivarTerminalViewModel viewModel)
    {
        var dialogo = new ActivarTerminalDialog(viewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        return dialogo.ShowDialog() == true;
    }
}
