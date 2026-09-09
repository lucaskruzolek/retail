using System.Windows;
using Wpf.Ui.Controls;

namespace Retail.App;

/// <summary>
/// Ventana principal de arranque con selector rápido hacia la Galería de Controles (Etapa 0.6).
/// </summary>
public partial class MainWindow : FluentWindow
{
    private bool _mostrarGaleria;

    public MainWindow()
    {
        InitializeComponent();

        // Salvaguarda: limitar dimensiones al área de trabajo útil de la pantalla
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
    }

    private void BtnToggleVista_Click(object sender, RoutedEventArgs e)
    {
        _mostrarGaleria = !_mostrarGaleria;

        if (_mostrarGaleria)
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelGaleria.Visibility = Visibility.Visible;
            TxtToggleIcon.Text = "🏠";
            TxtToggleText.Text = "Volver al Panel Principal";
        }
        else
        {
            PanelGaleria.Visibility = Visibility.Collapsed;
            PanelBienvenida.Visibility = Visibility.Visible;
            TxtToggleIcon.Text = "🎨";
            TxtToggleText.Text = "Ver Galería de Controles (Etapa 0.6)";
        }
    }
}
