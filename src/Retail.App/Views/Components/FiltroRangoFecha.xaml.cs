using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Botón con el rango de fechas elegido que abre el panel <see cref="SelectorRangoFecha"/>: el filtro de período de las
/// barras de filtros que no están en la cabecera de una grilla (el tablero gerencial). El estado vive en
/// <see cref="FiltroFechaColumnaViewModel"/> y la mecánica del popup en <see cref="PanelFiltroEmergente"/>, la misma de
/// <see cref="FiltroColumna"/> y <see cref="FiltroFechaColumna"/>.
/// </summary>
public partial class FiltroRangoFecha : UserControl
{
    public static readonly DependencyProperty FiltroProperty =
        DependencyProperty.Register(
            nameof(Filtro),
            typeof(FiltroFechaColumnaViewModel),
            typeof(FiltroRangoFecha),
            new PropertyMetadata(null, AlCambiarFiltro));

    private readonly PanelFiltroEmergente _panel;

    public FiltroRangoFecha()
    {
        InitializeComponent();

        _panel = new PanelFiltroEmergente(this, Panel, Tarjeta, Boton, () => Tarjeta.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
    }

    public FiltroFechaColumnaViewModel? Filtro
    {
        get => (FiltroFechaColumnaViewModel?)GetValue(FiltroProperty);
        set => SetValue(FiltroProperty, value);
    }

    private static void AlCambiarFiltro(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FiltroRangoFecha)d;
        control._panel.Vincular(e.OldValue as FiltroFechaColumnaViewModel, e.NewValue as FiltroFechaColumnaViewModel);
    }
}
