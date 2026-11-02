using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Retail.App.Navegacion;

namespace Retail.App.Views.Components;

/// <summary>
/// Selector de sección: bajo el título de una página, ofrece las demás páginas de su sección (p. ej. Pedidos / Ingresos /
/// Proveedores en Compras) como un <see cref="SelectorSegmentado"/>. Con una sola página visible para el rol, o ninguna,
/// el control se colapsa: no hay nada entre qué elegir.
/// <para>
/// Regla de oro: navega SOLO cuando el operador hace clic (o usa el teclado) sobre una opción, es decir con el evento
/// <see cref="System.Windows.Controls.Primitives.ButtonBase.Click"/>. Marcar una opción por código (al cargar, al
/// reconstruir o al reaccionar a <see cref="INavegacionSecciones.PaginaActualCambio"/>) no navega; de lo contrario cada
/// cambio de página dispararía otra navegación y se armaría un ciclo.
/// </para>
/// </summary>
public partial class NavegadorSeccion : UserControl
{
    public static readonly DependencyProperty SeccionIdProperty =
        DependencyProperty.Register(
            nameof(SeccionId),
            typeof(string),
            typeof(NavegadorSeccion),
            new PropertyMetadata(string.Empty, AlCambiarOrigen));

    public static readonly DependencyProperty NavegacionProperty =
        DependencyProperty.Register(
            nameof(Navegacion),
            typeof(INavegacionSecciones),
            typeof(NavegadorSeccion),
            new PropertyMetadata(null, AlCambiarOrigen));

    // Agrupa las opciones de esta instancia: sin nombre propio, dos navegadores en pantalla se desmarcarían entre sí
    private readonly string _grupo = "NavegadorSeccion_" + Guid.NewGuid().ToString("N");

    // La navegación resuelta del contenedor de dependencias cuando Navegacion no se asignó
    private INavegacionSecciones? _resuelta;
    private INavegacionSecciones? _suscripta;

    public NavegadorSeccion()
    {
        InitializeComponent();

        Visibility = Visibility.Collapsed;
        Loaded += (_, _) => Conectar();
        Unloaded += (_, _) => Desconectar();
    }

    /// <summary>Identificador de la sección cuyas páginas se ofrecen (p. ej. "compras").</summary>
    public string SeccionId
    {
        get => (string)GetValue(SeccionIdProperty);
        set => SetValue(SeccionIdProperty, value);
    }

    /// <summary>Navegación del shell. Si es <c>null</c>, se intenta resolver del contenedor de dependencias al cargar.</summary>
    public INavegacionSecciones? Navegacion
    {
        get => (INavegacionSecciones?)GetValue(NavegacionProperty);
        set => SetValue(NavegacionProperty, value);
    }

    private static void AlCambiarOrigen(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var navegador = (NavegadorSeccion)d;

        if (navegador.IsLoaded)
        {
            navegador.Conectar();
        }
    }

    private void Conectar()
    {
        Desconectar();

        _resuelta = Navegacion is null ? ResolverDelContenedor() : null;

        INavegacionSecciones? navegacion = Navegacion ?? _resuelta;
        if (navegacion is not null)
        {
            navegacion.PaginaActualCambio += AlCambiarPaginaActual;
            _suscripta = navegacion;
        }

        Reconstruir();
    }

    // Se suelta la suscripción al descargar la vista: el shell vive más que la página y la retendría en memoria
    private void Desconectar()
    {
        if (_suscripta is not null)
        {
            _suscripta.PaginaActualCambio -= AlCambiarPaginaActual;
            _suscripta = null;
        }
    }

    // Tolerante: en una prueba o en la galería no hay contenedor (App.Services no existe o no registra el servicio)
    private static INavegacionSecciones? ResolverDelContenedor()
    {
        try
        {
            return App.Services.GetService<INavegacionSecciones>();
        }
        catch (Exception ex) when (ex is InvalidCastException or NullReferenceException or InvalidOperationException)
        {
            return null;
        }
    }

    private void AlCambiarPaginaActual()
    {
        if (Dispatcher.CheckAccess())
        {
            MarcarPaginaActual();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(MarcarPaginaActual));
        }
    }

    private void Reconstruir()
    {
        Opciones.Children.Clear();

        INavegacionSecciones? navegacion = _suscripta;
        IReadOnlyList<PaginaNavegacion> paginas = navegacion is null || string.IsNullOrEmpty(SeccionId)
            ? []
            : navegacion.PaginasVisibles(SeccionId);

        if (paginas.Count < 2)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var estiloOpcion = (Style)FindResource("SegmentedRadioButtonStyle");

        foreach (PaginaNavegacion pagina in paginas)
        {
            string tooltip = pagina.Atajo is null ? pagina.Titulo : $"{pagina.Titulo} ({pagina.Atajo})";

            var opcion = new RadioButton
            {
                GroupName = _grupo,
                Style = estiloOpcion,
                Content = pagina.Titulo,
                Tag = pagina.Id,
                ToolTip = tooltip
            };
            AutomationProperties.SetName(opcion, pagina.Titulo);
            opcion.Click += AlHacerClicEnOpcion;

            Opciones.Children.Add(opcion);
        }

        Opciones.Columns = paginas.Count;
        Visibility = Visibility.Visible;
        MarcarPaginaActual();
    }

    // Solo marca la opción de la página abierta; asignar IsChecked por código no dispara Click, así que no navega
    private void MarcarPaginaActual()
    {
        string? actual = _suscripta?.PaginaActualId;

        foreach (RadioButton opcion in Opciones.Children.OfType<RadioButton>())
        {
            opcion.IsChecked = actual is not null && (string)opcion.Tag == actual;
        }
    }

    private void AlHacerClicEnOpcion(object sender, RoutedEventArgs e)
    {
        var opcion = (RadioButton)sender;
        var paginaId = (string)opcion.Tag;

        if (_suscripta is not null && paginaId != _suscripta.PaginaActualId)
        {
            _suscripta.IrA(paginaId);
        }

        // Si la navegación no cambió de página (p. ej. sin acceso), la marca vuelve a la página abierta
        MarcarPaginaActual();
    }
}
