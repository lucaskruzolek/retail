using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Retail.App.Views.Components;

/// <summary>
/// Selector segmentado (p. ej. Producto / Servicio): un riel gris con un único "indicador" que se desliza hasta la opción
/// marcada. El reparto de responsabilidades es:
/// <list type="bullet">
/// <item><description>Cada <see cref="RadioButton"/> (estilo <c>SegmentedRadioButtonStyle</c>) solo dibuja su texto: conserva
/// su binding, su <c>IsEnabled</c>, su tooltip y las flechas del teclado dentro del grupo.</description></item>
/// <item><description>Este control dibuja el riel y el indicador (<c>PART_Indicador</c>, definido en su plantilla de
/// <c>Controls.xaml</c>). Si cada opción pintara su propio fondo, la selección "saltaría"; con un solo indicador se puede
/// animar su posición y su ancho.</description></item>
/// </list>
/// El contenido es libre (normalmente un <c>UniformGrid</c> de RadioButtons): el control busca en el árbol visual cuál está
/// marcado, mide dónde quedó y mueve el indicador hasta ahí. El estilo se aplica por tipo (implícito), sin
/// <c>Themes/Generic.xaml</c>.
/// </summary>
public class SelectorSegmentado : ContentControl
{
    private const string NombreIndicador = "PART_Indicador";
    private const string NombreCapas = "PART_Capas";

    public static readonly DependencyProperty AnimarCambiosProperty =
        DependencyProperty.Register(
            nameof(AnimarCambios),
            typeof(bool),
            typeof(SelectorSegmentado),
            new PropertyMetadata(true));

    private FrameworkElement? _indicador;
    private FrameworkElement? _capas;

    public SelectorSegmentado()
    {
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(AlMarcarUnaOpcion));
        SizeChanged += (_, _) => Reposicionar(animar: false);
        Loaded += (_, _) => ReposicionarCuandoHayMedidas();

        // Una página alojada en un Frame o en un contenedor que estaba oculto puede cargarse antes de tener medidas
        // (el ancho de la opción marcada vale cero): al hacerse visible se vuelve a posicionar
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                ReposicionarCuandoHayMedidas();
            }
        };
    }

    // Se posiciona ya y otra vez cuando termina el layout pendiente: lo que se marca por binding durante la carga
    // (IsChecked enlazado al ViewModel) se mide recién después
    private void ReposicionarCuandoHayMedidas()
    {
        Reposicionar(animar: false);
        Dispatcher.BeginInvoke(
            new Action(() => Reposicionar(animar: false)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Si es falso, el indicador salta sin animación (las pruebas lo apagan para medir la posición final).</summary>
    public bool AnimarCambios
    {
        get => (bool)GetValue(AnimarCambiosProperty);
        set => SetValue(AnimarCambiosProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _indicador = GetTemplateChild(NombreIndicador) as FrameworkElement;
        _capas = GetTemplateChild(NombreCapas) as FrameworkElement;

        if (_indicador is not null)
        {
            // Se crea acá y no en la plantilla: WPF congela los Freezable de una plantilla (compartidos entre instancias)
            // y un TranslateTransform congelado no se puede mover ni animar.
            _indicador.RenderTransform = new TranslateTransform();
        }
    }

    private void AlMarcarUnaOpcion(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return; // Loaded y SizeChanged posicionan el indicador cuando ya hay medidas
        }

        // Marcar desde código puede dejar el layout pendiente; sin medidas actuales TranslatePoint daría cero
        UpdateLayout();
        Reposicionar(animar: true);
    }

    private void Reposicionar(bool animar)
    {
        if (_indicador is null || _capas is null)
        {
            return;
        }

        RadioButton? marcada = BuscarOpcionMarcada(this);

        if (marcada is null || marcada.ActualWidth <= 0)
        {
            _indicador.Visibility = Visibility.Collapsed;
            return;
        }

        bool eraVisible = _indicador.Visibility == Visibility.Visible;
        _indicador.Visibility = Visibility.Visible;

        // Posición de la opción dentro de la capa del indicador: ese es el sistema de coordenadas del TranslateTransform
        Point origen = marcada.TranslatePoint(new Point(0, 0), _capas);
        _indicador.Height = marcada.ActualHeight;

        var traslado = (TranslateTransform)_indicador.RenderTransform;
        traslado.Y = origen.Y;

        if (animar && eraVisible && AnimarCambios && SystemParameters.ClientAreaAnimation)
        {
            traslado.BeginAnimation(TranslateTransform.XProperty, CrearAnimacion(origen.X));
            _indicador.BeginAnimation(WidthProperty, CrearAnimacion(marcada.ActualWidth));
            return;
        }

        // Primer posicionamiento o cambio de tamaño: se fija el valor directo, soltando cualquier animación en curso
        traslado.BeginAnimation(TranslateTransform.XProperty, null);
        _indicador.BeginAnimation(WidthProperty, null);
        traslado.X = origen.X;
        _indicador.Width = marcada.ActualWidth;
    }

    private DoubleAnimation CrearAnimacion(double destino)
    {
        Duration duracion = TryFindResource("DuracionTransicion") is Duration d ? d : new Duration(TimeSpan.FromMilliseconds(180));

        return new DoubleAnimation(destino, duracion)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
    }

    // Recorre el árbol visual del contenido; no entra dentro de un RadioButton (su plantilla no tiene otras opciones)
    private static RadioButton? BuscarOpcionMarcada(DependencyObject raiz)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            DependencyObject hijo = VisualTreeHelper.GetChild(raiz, i);

            if (hijo is RadioButton opcion)
            {
                if (opcion.IsChecked == true)
                {
                    return opcion;
                }

                continue;
            }

            RadioButton? encontrada = BuscarOpcionMarcada(hijo);

            if (encontrada is not null)
            {
                return encontrada;
            }
        }

        return null;
    }
}
