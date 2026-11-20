using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Dialogs;

/// <summary>
/// Pregunta previa a restaurar con otras terminales conectadas (MT-12): cuántos minutos darles para terminar la venta en
/// curso (0 = ahora). Cerrarla con la X, con Esc o con "Cancelar" deja <see cref="Minutos"/> en <c>null</c>.
/// </summary>
public partial class ProgramarMantenimientoDialog : FluentWindow
{
    public ProgramarMantenimientoDialog(int terminalesConectadas)
    {
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;

        InitializeComponent();

        TxtPregunta.Text = $"Hay {terminalesConectadas} terminal(es) conectada(s). Se les avisará que se va a restaurar la base; las ventas en curso pueden terminar.";
    }

    /// <summary>Minutos elegidos; <c>null</c> si el operador canceló.</summary>
    public int? Minutos { get; private set; }

    private void OnProgramarClick(object sender, RoutedEventArgs e)
    {
        var elegida = OpcionesMinutos.Children.OfType<RadioButton>().FirstOrDefault(opcion => opcion.IsChecked == true);
        if (elegida is not null)
        {
            Minutos = int.Parse((string)elegida.Tag, CultureInfo.InvariantCulture);
        }

        Close();
    }

    private void OnCancelarClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
