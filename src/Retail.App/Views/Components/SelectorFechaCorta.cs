using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace Retail.App.Views.Components;

/// <summary>
/// <c>DatePicker</c> que siempre escribe la fecha como dd/MM/yyyy ("01/10/2026"). El formato corto de WPF sale de la
/// cultura del equipo (con ICU, es-AR da "1/10/2026" y otras culturas cambian el patrón), así que no es determinista.
/// El calendario y la selección no cambian; solo el texto que se muestra en la caja.
/// </summary>
public class SelectorFechaCorta : DatePicker
{
    private const string Formato = "dd/MM/yyyy";

    private DatePickerTextBox? _caja;

    static SelectorFechaCorta()
    {
        // El estilo implícito de WPF-UI se busca por el tipo exacto (DatePicker); sin esto, la subclase cae al estilo clásico.
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectorFechaCorta), new FrameworkPropertyMetadata(typeof(DatePicker)));
    }

    public SelectorFechaCorta()
    {
        SelectedDateChanged += (_, _) => Reformatear();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_caja is not null)
        {
            _caja.LostFocus -= CajaPerdioElFoco;
        }

        _caja = GetTemplateChild("PART_TextBox") as DatePickerTextBox;
        if (_caja is not null)
        {
            _caja.LostFocus += CajaPerdioElFoco;
        }

        Reformatear();
    }

    private void CajaPerdioElFoco(object sender, System.Windows.RoutedEventArgs e) => Reformatear();

    /// <summary>
    /// Reescribe el texto de la caja con el formato fijo. Se difiere porque <c>DatePicker</c> vuelve a escribir el texto
    /// con la cultura del equipo después de interpretar lo tipeado o elegido.
    /// </summary>
    private void Reformatear()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            () =>
            {
                if (_caja is not null && SelectedDate is { } fecha)
                {
                    var texto = fecha.ToString(Formato, CultureInfo.InvariantCulture);
                    if (_caja.Text != texto)
                    {
                        _caja.Text = texto;
                    }
                }
            });
    }
}
