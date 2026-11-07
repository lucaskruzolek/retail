using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Título de una columna con su filtro de selección múltiple: un embudo que abre un panel con buscador, casillas
/// (agrupadas si el filtro tiene grupos) y Cancelar / Aplicar. Se usa como <c>Header</c> de una columna de
/// <c>DataGrid</c>; el estado vive en <see cref="FiltroColumnaViewModel"/> y la mecánica del popup (clic afuera, animación,
/// teclado) en <see cref="PanelFiltroEmergente"/>, que comparte con <see cref="FiltroFechaColumna"/>.
/// </summary>
public partial class FiltroColumna : UserControl
{
    public static readonly DependencyProperty FiltroProperty =
        DependencyProperty.Register(
            nameof(Filtro),
            typeof(FiltroColumnaViewModel),
            typeof(FiltroColumna),
            new PropertyMetadata(null, AlCambiarFiltro));

    private readonly PanelFiltroEmergente _panel;

    public FiltroColumna()
    {
        InitializeComponent();

        _panel = new PanelFiltroEmergente(this, Panel, Tarjeta, BotonFiltro, EnfocarAlAbrir);
    }

    public FiltroColumnaViewModel? Filtro
    {
        get => (FiltroColumnaViewModel?)GetValue(FiltroProperty);
        set => SetValue(FiltroProperty, value);
    }

    private static void AlCambiarFiltro(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FiltroColumna)d;
        control._panel.Vincular(e.OldValue as FiltroColumnaViewModel, e.NewValue as FiltroColumnaViewModel);
    }

    // Con buscador el foco va a él; sin buscador, a la primera casilla
    private void EnfocarAlAbrir()
    {
        if (Busqueda.IsVisible)
        {
            Busqueda.Focus();
        }
        else
        {
            Tarjeta.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }
}
