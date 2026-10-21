using System.Windows;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Code-behind del formulario modal de alta y edición de rubros, categorías y marcas.
/// </summary>
public partial class ClasificacionFormDialog : FluentWindow
{
    public ClasificacionFormDialog()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }
}
