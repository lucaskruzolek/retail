using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Título de una columna de fecha con su filtro de rango: un botón de calendario que abre un panel
/// (<see cref="SelectorRangoFecha"/>) con atajos, dos calendarios y Cancelar / Aplicar. Se usa como <c>Header</c> de una
/// columna de <c>DataGrid</c>; el estado vive en <see cref="FiltroFechaColumnaViewModel"/> y la mecánica del popup en
/// <see cref="PanelFiltroEmergente"/>, la misma de <see cref="FiltroColumna"/>.
/// </summary>
public partial class FiltroFechaColumna : UserControl
{
    public static readonly DependencyProperty FiltroProperty =
        DependencyProperty.Register(
            nameof(Filtro),
            typeof(FiltroFechaColumnaViewModel),
            typeof(FiltroFechaColumna),
            new PropertyMetadata(null, AlCambiarFiltro));

    private readonly PanelFiltroEmergente _panel;

    public FiltroFechaColumna()
    {
        InitializeComponent();

        _panel = new PanelFiltroEmergente(this, Panel, Tarjeta, BotonFiltro, () => Tarjeta.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
    }

    public FiltroFechaColumnaViewModel? Filtro
    {
        get => (FiltroFechaColumnaViewModel?)GetValue(FiltroProperty);
        set => SetValue(FiltroProperty, value);
    }

    private static void AlCambiarFiltro(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FiltroFechaColumna)d;
        control._panel.Vincular(e.OldValue as FiltroFechaColumnaViewModel, e.NewValue as FiltroFechaColumnaViewModel);
    }
}
