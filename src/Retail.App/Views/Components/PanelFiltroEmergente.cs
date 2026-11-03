using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Mecánica del panel emergente de un filtro de columna, común a <see cref="FiltroColumna"/> y
/// <see cref="FiltroFechaColumna"/>. El panel es un <see cref="Popup"/> con <c>StaysOpen=True</c> que abre y cierra esta
/// clase, no WPF. Con <c>StaysOpen=False</c> el clic que cierra el panel sigue de largo hasta el elemento de abajo: la
/// cabecera ordenaba la columna. Acá el clic de afuera (el que cae fuera del panel) se intercepta en la ventana
/// (<c>PreviewMouseDown</c>), cierra el panel y se marca como atendido: el clic que cierra no hace nada más. Controlar el
/// cierre permite además animarlo.
/// </summary>
internal sealed class PanelFiltroEmergente
{
    // Entrada y salida del panel: corta, con un leve desplazamiento vertical (DuracionTransicion la fija el tema)
    private const double DesplazamientoAnimacion = -4;

    private readonly FrameworkElement _propietario;
    private readonly Popup _panel;
    private readonly Border _tarjeta;
    private readonly Action _enfocarAlAbrir;
    private readonly UIElement _botonDeRetorno;
    private readonly TranslateTransform _desplazamiento = new();
    private IFiltroEmergente? _filtro;
    private Window? _ventana;

    public PanelFiltroEmergente(
        FrameworkElement propietario,
        Popup panel,
        Border tarjeta,
        UIElement botonDeRetorno,
        Action enfocarAlAbrir)
    {
        _propietario = propietario;
        _panel = panel;
        _tarjeta = tarjeta;
        _botonDeRetorno = botonDeRetorno;
        _enfocarAlAbrir = enfocarAlAbrir;

        _tarjeta.RenderTransform = _desplazamiento;
        _tarjeta.MouseLeftButtonDown += MarcarComoAtendido;
        _tarjeta.MouseLeftButtonUp += MarcarComoAtendido;
        _tarjeta.PreviewKeyDown += AlPresionarTecla;
        _propietario.Unloaded += (_, _) => CerrarPanel(animar: false);
    }

    /// <summary>Cambia el filtro que gobierna el panel (al enlazar o cambiar la propiedad <c>Filtro</c> del control).</summary>
    public void Vincular(IFiltroEmergente? anterior, IFiltroEmergente? nuevo)
    {
        if (anterior is not null)
        {
            anterior.PropertyChanged -= AlCambiarEstadoDelFiltro;
        }

        _filtro = nuevo;

        if (nuevo is not null)
        {
            nuevo.PropertyChanged += AlCambiarEstadoDelFiltro;
        }
    }

    private void AlCambiarEstadoDelFiltro(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IFiltroEmergente.EstaAbierto) || _filtro is null)
        {
            return;
        }

        if (_filtro.EstaAbierto)
        {
            AbrirPanel();
        }
        else
        {
            CerrarPanel(animar: true);
        }
    }

    private void AbrirPanel()
    {
        _ventana = Window.GetWindow(_propietario);
        if (_ventana is not null)
        {
            _ventana.PreviewMouseDown += AlHacerClicEnLaVentana;
            _ventana.Deactivated += AlDesactivarseLaVentana;
            _ventana.LocationChanged += AlMoverseLaVentana;
        }

        _panel.IsOpen = true;
        Animar(opacidad: 1, desplazamiento: 0, desde: (0, DesplazamientoAnimacion), alTerminar: null);

        // El foco se da cuando el Popup ya tiene su ventana nativa
        _propietario.Dispatcher.BeginInvoke(_enfocarAlAbrir, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CerrarPanel(bool animar)
    {
        if (_ventana is not null)
        {
            _ventana.PreviewMouseDown -= AlHacerClicEnLaVentana;
            _ventana.Deactivated -= AlDesactivarseLaVentana;
            _ventana.LocationChanged -= AlMoverseLaVentana;
            _ventana = null;
        }

        if (!_panel.IsOpen)
        {
            return;
        }

        if (!animar || !SystemParameters.ClientAreaAnimation)
        {
            _panel.IsOpen = false;
            return;
        }

        Animar(opacidad: 0, desplazamiento: DesplazamientoAnimacion, desde: null, alTerminar: () =>
        {
            // Si mientras se desvanecía se volvió a abrir, no se cierra
            if (_filtro?.EstaAbierto != true)
            {
                _panel.IsOpen = false;
            }
        });
    }

    private void Animar(double opacidad, double desplazamiento, (double Opacidad, double Desplazamiento)? desde, Action? alTerminar)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            _tarjeta.BeginAnimation(UIElement.OpacityProperty, null);
            _desplazamiento.BeginAnimation(TranslateTransform.YProperty, null);
            _tarjeta.Opacity = opacidad;
            _desplazamiento.Y = desplazamiento;
            alTerminar?.Invoke();
            return;
        }

        Duration duracion = _propietario.TryFindResource("DuracionTransicion") is Duration d ? d : new Duration(TimeSpan.FromMilliseconds(180));
        var suavizado = new CubicEase { EasingMode = EasingMode.EaseOut };

        var animacionOpacidad = new DoubleAnimation(opacidad, duracion) { EasingFunction = suavizado };
        var animacionDesplazamiento = new DoubleAnimation(desplazamiento, duracion) { EasingFunction = suavizado };
        if (desde is { } inicio)
        {
            animacionOpacidad.From = inicio.Opacidad;
            animacionDesplazamiento.From = inicio.Desplazamiento;
        }

        if (alTerminar is not null)
        {
            animacionOpacidad.Completed += (_, _) => alTerminar();
        }

        _tarjeta.BeginAnimation(UIElement.OpacityProperty, animacionOpacidad);
        _desplazamiento.BeginAnimation(TranslateTransform.YProperty, animacionDesplazamiento);
    }

    // Un clic en la ventana, fuera del panel, lo cierra descartando el borrador, como Cancelar. Se marca como atendido
    // para que no ordene la columna ni reabra el panel con el clic sobre el embudo. Los clics DENTRO del panel también
    // llegan acá (el contenido de un Popup sube por el árbol lógico hasta la ventana), así que se dejan pasar.
    private void AlHacerClicEnLaVentana(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject origen && EstaDentroDelPanel(origen))
        {
            return;
        }

        _filtro?.CancelarCommand.Execute(null);
        e.Handled = true;
    }

    // Sube desde el elemento clicado hasta la tarjeta del panel. Un texto (Run) no es Visual, por eso se usa el árbol
    // lógico cuando el visual no alcanza.
    private bool EstaDentroDelPanel(DependencyObject origen)
    {
        DependencyObject? actual = origen;
        while (actual is not null)
        {
            if (ReferenceEquals(actual, _tarjeta))
            {
                return true;
            }

            actual = actual is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(actual) ?? LogicalTreeHelper.GetParent(actual)
                : LogicalTreeHelper.GetParent(actual);
        }

        return false;
    }

    private void AlDesactivarseLaVentana(object? sender, EventArgs e)
    {
        _filtro?.CancelarCommand.Execute(null);
    }

    // El Popup queda en su lugar de la pantalla aunque la ventana se mueva: se cierra para no quedar flotando
    private void AlMoverseLaVentana(object? sender, EventArgs e)
    {
        _filtro?.CancelarCommand.Execute(null);
    }

    private static void MarcarComoAtendido(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    // Enter aplica y Esc cancela desde cualquier parte del panel, como en un diálogo. Al cerrar con el teclado el foco
    // vuelve al embudo.
    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (_filtro is null || (e.Key != Key.Enter && e.Key != Key.Escape))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            // Un filtro que todavía no se puede aplicar (falta un extremo del rango) sigue abierto
            if (!_filtro.AplicarCommand.CanExecute(null))
            {
                e.Handled = true;
                return;
            }

            _filtro.AplicarCommand.Execute(null);
        }
        else
        {
            _filtro.CancelarCommand.Execute(null);
        }

        e.Handled = true;
        _botonDeRetorno.Focus();
    }
}
