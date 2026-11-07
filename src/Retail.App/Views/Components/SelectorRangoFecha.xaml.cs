using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Panel de selección de un rango de fechas: atajos ("Hoy", "Esta semana"…), dos calendarios y las acciones Limpiar,
/// Cancelar y Aplicar. Trabaja sobre un <c>FiltroFechaColumnaViewModel</c> (su DataContext) y lo aloja
/// <see cref="FiltroFechaColumna"/> en su popup.
/// </summary>
public partial class SelectorRangoFecha : UserControl
{
    public SelectorRangoFecha()
    {
        InitializeComponent();

        CalendarioDesde.PreviewMouseUp += LiberarCapturaDelCalendario;
        CalendarioHasta.PreviewMouseUp += LiberarCapturaDelCalendario;
    }

    // Un Calendar captura el mouse al elegir un día y el primer clic siguiente, en cualquier otro control, se pierde: se
    // libera la captura al soltar el botón (el defecto conocido de System.Windows.Controls.Calendar)
    private static void LiberarCapturaDelCalendario(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is CalendarItem)
        {
            Mouse.Capture(null);
        }
    }
}
