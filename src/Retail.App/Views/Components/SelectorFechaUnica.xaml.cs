using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Botón con una fecha que abre un calendario: el selector de fecha única del sistema de diseño (el comprobante de un
/// ingreso). Es el hermano de <see cref="FiltroRangoFecha"/>, que elige un rango. La propiedad <see cref="Fecha"/> se
/// enlaza en ambos sentidos; la mecánica del popup es la de <see cref="PanelFiltroEmergente"/>.
/// </summary>
public partial class SelectorFechaUnica : UserControl
{
    public static readonly DependencyProperty FechaProperty =
        DependencyProperty.Register(
            nameof(Fecha),
            typeof(DateTime),
            typeof(SelectorFechaUnica),
            new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AlCambiarFecha));

    public static readonly DependencyProperty NombreAutomatizacionProperty =
        DependencyProperty.Register(nameof(NombreAutomatizacion), typeof(string), typeof(SelectorFechaUnica), new PropertyMetadata("Fecha"));

    public SelectorFechaUnica()
    {
        InitializeComponent();

        var panel = new PanelFiltroEmergente(this, Panel, Tarjeta, Boton, () => Calendario.Focus());
        panel.Vincular(null, Estado);

        Calendario.SelectedDatesChanged += AlElegirDia;
        Calendario.PreviewMouseUp += LiberarCapturaDelCalendario;
        SincronizarCalendario();
    }

    public PanelFechaUnicaViewModel Estado { get; } = new();

    public DateTime Fecha
    {
        get => (DateTime)GetValue(FechaProperty);
        set => SetValue(FechaProperty, value);
    }

    /// <summary>Nombre que anuncian los lectores de pantalla para el botón.</summary>
    public string NombreAutomatizacion
    {
        get => (string)GetValue(NombreAutomatizacionProperty);
        set => SetValue(NombreAutomatizacionProperty, value);
    }

    private static void AlCambiarFecha(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SelectorFechaUnica)d).SincronizarCalendario();
    }

    private void SincronizarCalendario()
    {
        if (Calendario.SelectedDate != Fecha.Date)
        {
            Calendario.SelectedDate = Fecha.Date;
        }

        Calendario.DisplayDate = Fecha.Date;
    }

    private void AlElegirDia(object? sender, SelectionChangedEventArgs e)
    {
        if (Calendario.SelectedDate is { } dia && dia != Fecha.Date)
        {
            Fecha = dia;
        }

        Estado.EstaAbierto = false;
    }

    // Un Calendar captura el mouse al elegir un día y el primer clic siguiente, en cualquier otro control, se pierde: se
    // libera la captura al soltar el botón (el mismo defecto que maneja SelectorRangoFecha)
    private static void LiberarCapturaDelCalendario(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is CalendarItem)
        {
            Mouse.Capture(null);
        }
    }
}
