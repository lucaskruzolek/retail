using System.Windows;
using System.Windows.Input;

namespace Retail.App.Helpers;

/// <summary>
/// Comportamiento de una franja que hace de barra de título sin serlo: arrastrarla mueve la ventana y el doble clic la
/// maximiza o la restaura. Lo comparten la barra de los diálogos (<c>EncabezadoDialogo</c>) y la fila del título de las
/// páginas (<c>PageHeader</c>), que en la ventana principal ocupa el lugar de la barra de Windows.
/// </summary>
public static class ArrastreVentana
{
    /// <summary>Atiende el botón izquierdo presionado sobre la franja.</summary>
    /// <param name="origen">Elemento de la franja, para encontrar su ventana.</param>
    /// <param name="e">Evento del mouse; se marca como atendido si mueve o maximiza la ventana.</param>
    /// <param name="permitirMaximizar">El doble clic maximiza o restaura (solo ventanas redimensionables).</param>
    public static void AlPresionar(DependencyObject origen, MouseButtonEventArgs e, bool permitirMaximizar)
    {
        ArgumentNullException.ThrowIfNull(origen);
        ArgumentNullException.ThrowIfNull(e);

        var ventana = Window.GetWindow(origen);
        if (ventana is null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            if (permitirMaximizar)
            {
                AlternarMaximizado(ventana);
                e.Handled = true;
            }

            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            ventana.DragMove();
            e.Handled = true;
        }
    }

    /// <summary>Maximiza la ventana o, si ya lo está, la devuelve a su tamaño normal.</summary>
    public static void AlternarMaximizado(Window ventana)
    {
        ArgumentNullException.ThrowIfNull(ventana);

        ventana.WindowState = ventana.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
