using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Retail.App.ViewModels.Comun;

namespace Retail.App.Views.Components;

/// <summary>
/// Título de una columna con su filtro de selección múltiple: un embudo que abre un panel con buscador, casillas
/// (agrupadas si el filtro tiene grupos) y Cancelar / Aplicar. Se usa como <c>Header</c> de una columna de
/// <c>DataGrid</c>; el estado vive en <see cref="FiltroColumnaViewModel"/>.
/// <para>
/// El panel es un <see cref="System.Windows.Controls.Primitives.Popup"/> con <c>StaysOpen=True</c> que abre y cierra este
/// control, no WPF. Con <c>StaysOpen=False</c> el clic que cierra el panel sigue de largo hasta el elemento de abajo: la
/// cabecera ordenaba la columna. Acá el clic de afuera (el que cae fuera del panel) se intercepta en la ventana (<c>PreviewMouseDown</c>), cierra
/// el panel y se marca como atendido: el clic que cierra no hace nada más. Controlar el cierre permite además animarlo.
/// </para>
/// </summary>
public partial class FiltroColumna : UserControl
{
    public static readonly DependencyProperty FiltroProperty =
        DependencyProperty.Register(
            nameof(Filtro),
            typeof(FiltroColumnaViewModel),
            typeof(FiltroColumna),
            new PropertyMetadata(null, AlCambiarFiltro));

    // Entrada y salida del panel: corta, con un leve desplazamiento vertical (DuracionTransicion la fija el tema)
    private const double DesplazamientoAnimacion = -4;

    private readonly TranslateTransform _desplazamiento = new();
    private Window? _ventana;

    public FiltroColumna()
    {
        InitializeComponent();

        Tarjeta.RenderTransform = _desplazamiento;
        Tarjeta.MouseLeftButtonDown += MarcarComoAtendido;
        Tarjeta.MouseLeftButtonUp += MarcarComoAtendido;
        Tarjeta.PreviewKeyDown += AlPresionarTecla;
        Unloaded += (_, _) => CerrarPanel(animar: false);
    }

    public FiltroColumnaViewModel? Filtro
    {
        get => (FiltroColumnaViewModel?)GetValue(FiltroProperty);
        set => SetValue(FiltroProperty, value);
    }

    private static void AlCambiarFiltro(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FiltroColumna)d;
        if (e.OldValue is FiltroColumnaViewModel anterior)
        {
            anterior.PropertyChanged -= control.AlCambiarEstadoDelFiltro;
        }

        if (e.NewValue is FiltroColumnaViewModel nuevo)
        {
            nuevo.PropertyChanged += control.AlCambiarEstadoDelFiltro;
        }
    }

    private void AlCambiarEstadoDelFiltro(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FiltroColumnaViewModel.EstaAbierto) || Filtro is null)
        {
            return;
        }

        if (Filtro.EstaAbierto)
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
        _ventana = Window.GetWindow(this);
        if (_ventana is not null)
        {
            _ventana.PreviewMouseDown += AlHacerClicEnLaVentana;
            _ventana.Deactivated += AlDesactivarseLaVentana;
            _ventana.LocationChanged += AlMoverseLaVentana;
        }

        Panel.IsOpen = true;
        Animar(opacidad: 1, desplazamiento: 0, desde: (0, DesplazamientoAnimacion), alTerminar: null);

        // El foco se da cuando el Popup ya tiene su ventana nativa
        Dispatcher.BeginInvoke(() => Busqueda.Focus(), System.Windows.Threading.DispatcherPriority.Input);
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

        if (!Panel.IsOpen)
        {
            return;
        }

        if (!animar || !SystemParameters.ClientAreaAnimation)
        {
            Panel.IsOpen = false;
            return;
        }

        Animar(opacidad: 0, desplazamiento: DesplazamientoAnimacion, desde: null, alTerminar: () =>
        {
            // Si mientras se desvanecía se volvió a abrir, no se cierra
            if (Filtro?.EstaAbierto != true)
            {
                Panel.IsOpen = false;
            }
        });
    }

    private void Animar(double opacidad, double desplazamiento, (double Opacidad, double Desplazamiento)? desde, Action? alTerminar)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Tarjeta.BeginAnimation(OpacityProperty, null);
            _desplazamiento.BeginAnimation(TranslateTransform.YProperty, null);
            Tarjeta.Opacity = opacidad;
            _desplazamiento.Y = desplazamiento;
            alTerminar?.Invoke();
            return;
        }

        Duration duracion = TryFindResource("DuracionTransicion") is Duration d ? d : new Duration(TimeSpan.FromMilliseconds(180));
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

        Tarjeta.BeginAnimation(OpacityProperty, animacionOpacidad);
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

        Filtro?.CancelarCommand.Execute(null);
        e.Handled = true;
    }

    // Sube desde el elemento clicado hasta la tarjeta del panel. Un texto (Run) no es Visual, por eso se usa el árbol
    // lógico cuando el visual no alcanza.
    private bool EstaDentroDelPanel(DependencyObject origen)
    {
        DependencyObject? actual = origen;
        while (actual is not null)
        {
            if (ReferenceEquals(actual, Tarjeta))
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
        Filtro?.CancelarCommand.Execute(null);
    }

    // El Popup queda en su lugar de la pantalla aunque la ventana se mueva: se cierra para no quedar flotando
    private void AlMoverseLaVentana(object? sender, EventArgs e)
    {
        Filtro?.CancelarCommand.Execute(null);
    }

    private static void MarcarComoAtendido(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    // Enter aplica y Esc cancela desde cualquier parte del panel, como en un diálogo. Al cerrar con el teclado el foco
    // vuelve al embudo.
    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (Filtro is null || (e.Key != Key.Enter && e.Key != Key.Escape))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            Filtro.AplicarCommand.Execute(null);
        }
        else
        {
            Filtro.CancelarCommand.Execute(null);
        }

        e.Handled = true;
        BotonFiltro.Focus();
    }
}
