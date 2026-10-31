using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Code-behind del formulario modal de los diccionarios del motor de catalogación (términos, alias, prefijos y
/// pares). Sin lógica: el ViewModel gobierna el formulario y quien lo abre es <c>ClasificacionDialogService</c>.
/// </summary>
public partial class DiccionarioFormDialog : FluentWindow
{
    public DiccionarioFormDialog()
    {
        InitializeComponent();
    }
}
