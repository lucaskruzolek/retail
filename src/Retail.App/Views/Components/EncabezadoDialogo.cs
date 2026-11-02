using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Barra de título de un diálogo: el título y los botones de minimizar, maximizar (opcional) y cerrar en una banda que
/// comparte el fondo del diálogo. Reemplaza a la barra nativa: sus botones son íconos que solo se oscurecen en hover.
/// <para>
/// Responsabilidades (plantilla implícita en <c>Styles/Controls.xaml</c>):
/// <list type="bullet">
/// <item><description>Arrastrar la banda mueve la ventana (<see cref="Window.DragMove"/>); el doble clic maximiza o
/// restaura si <see cref="PermitirMaximizar"/>.</description></item>
/// <item><description><b>Esc cierra el diálogo</b>, igual que la ✕, si ningún control lo atendió antes (un desplegable
/// abierto, por ejemplo, lo usa para cerrarse). Todos los diálogos llevan este encabezado, así que la regla vale para
/// todos sin repetirla en cada uno. Enter, en cambio, lo resuelve el botón primario de cada diálogo con
/// <c>IsDefault="True"</c>.</description></item>
/// </list>
/// </para>
/// </summary>
public class EncabezadoDialogo : Control
{
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(EncabezadoDialogo), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PermitirMaximizarProperty =
        DependencyProperty.Register(nameof(PermitirMaximizar), typeof(bool), typeof(EncabezadoDialogo), new PropertyMetadata(false));

    private Window? _ventana;
    private FrameworkElement? _banda;
    private Button? _minimizar;
    private Button? _maximizar;
    private Button? _cerrar;

    public EncabezadoDialogo()
    {
        Loaded += (_, _) => EngancharVentana();
        Unloaded += (_, _) => SoltarVentana();
    }

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    /// <summary>Muestra el botón de maximizar (solo los diálogos con grillas grandes, como la incorporación de artículos).</summary>
    public bool PermitirMaximizar
    {
        get => (bool)GetValue(PermitirMaximizarProperty);
        set => SetValue(PermitirMaximizarProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_banda is not null)
        {
            _banda.MouseLeftButtonDown -= AlPresionarLaBanda;
        }

        if (_minimizar is not null)
        {
            _minimizar.Click -= AlMinimizar;
        }

        if (_maximizar is not null)
        {
            _maximizar.Click -= AlMaximizar;
        }

        if (_cerrar is not null)
        {
            _cerrar.Click -= AlCerrar;
        }

        base.OnApplyTemplate();

        _banda = GetTemplateChild("PART_Banda") as FrameworkElement;
        _minimizar = GetTemplateChild("PART_Minimizar") as Button;
        _maximizar = GetTemplateChild("PART_Maximizar") as Button;
        _cerrar = GetTemplateChild("PART_Cerrar") as Button;

        if (_banda is not null)
        {
            _banda.MouseLeftButtonDown += AlPresionarLaBanda;
        }

        if (_minimizar is not null)
        {
            _minimizar.Click += AlMinimizar;
        }

        if (_maximizar is not null)
        {
            _maximizar.Click += AlMaximizar;
        }

        if (_cerrar is not null)
        {
            _cerrar.Click += AlCerrar;
        }
    }

    private void EngancharVentana()
    {
        SoltarVentana();
        _ventana = Window.GetWindow(this);

        // Manejador del evento burbujeante: solo llega si ningún control del diálogo atendió la tecla antes
        _ventana?.AddHandler(KeyDownEvent, new KeyEventHandler(AlPresionarTecla));
    }

    private void SoltarVentana()
    {
        _ventana?.RemoveHandler(KeyDownEvent, new KeyEventHandler(AlPresionarTecla));
        _ventana = null;
    }

    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _ventana is null)
        {
            return;
        }

        // Si el diálogo tiene su Cancelar (IsCancel), WPF lo ejecuta con Esc después de este manejador: así corre su
        // lógica (DialogResult, limpieza) y, si está deshabilitado porque el diálogo trabaja, Esc no cierra. Solo cuando
        // no hay ninguno, Esc cierra como la ✕.
        if (TieneBotonCancelar(_ventana))
        {
            return;
        }

        e.Handled = true;
        _ventana.Close();
    }

    private static bool TieneBotonCancelar(DependencyObject raiz)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var hijo = System.Windows.Media.VisualTreeHelper.GetChild(raiz, i);
            if ((hijo is Button { IsCancel: true }) || TieneBotonCancelar(hijo))
            {
                return true;
            }
        }

        return false;
    }

    private void AlPresionarLaBanda(object sender, MouseButtonEventArgs e)
    {
        var ventana = Window.GetWindow(this);
        if (ventana is null)
        {
            return;
        }

        if (e.ClickCount == 2 && PermitirMaximizar)
        {
            AlternarMaximizado(ventana);
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            ventana.DragMove();
        }
    }

    private void AlMinimizar(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } ventana)
        {
            ventana.WindowState = WindowState.Minimized;
        }
    }

    private void AlMaximizar(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } ventana)
        {
            AlternarMaximizado(ventana);
        }
    }

    private void AlCerrar(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }

    private static void AlternarMaximizado(Window ventana)
    {
        ventana.WindowState = ventana.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
