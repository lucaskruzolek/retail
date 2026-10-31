using System.Windows;

namespace Retail.App.Views.Dev;

/// <summary>
/// Modal de referencia de la galería de estilos: muestra el patrón de diálogo (una sola superficie, secciones sin borde,
/// encabezado con Esc y pie con el botón primario como predeterminado) con el comportamiento real, sin persistir nada.
/// </summary>
public partial class DialogoMuestraDialog : Wpf.Ui.Controls.FluentWindow
{
    public DialogoMuestraDialog()
    {
        InitializeComponent();

        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
        Loaded += (_, _) => CampoNombre.Focus();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
