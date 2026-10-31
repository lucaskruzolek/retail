using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Barra superior de un listado: buscador, "Limpiar filtros", indicador de carga y cantidad de resultados. La página
/// la enlaza a su ViewModel por propiedades de dependencia (el texto de búsqueda en ambos sentidos).
/// <para>
/// Mientras <see cref="HaySeleccion"/> es verdadero, <see cref="AccionesDeSeleccion"/> (la <see cref="BulkActionBar"/> de
/// la página) reemplaza a "Limpiar filtros", la carga y el contador; el buscador sigue a la vista. Las acciones no tienen
/// padre lógico, así que heredan el DataContext por el árbol visual: el de la página, y sus comandos se enlazan directo.
/// </para>
/// </summary>
public partial class ListToolbar : UserControl
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly DependencyProperty TextoProperty =
        DependencyProperty.Register(
            nameof(Texto),
            typeof(string),
            typeof(ListToolbar),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(ListToolbar), new PropertyMetadata("Buscar..."));

    public static readonly DependencyProperty ComandoLimpiarProperty =
        DependencyProperty.Register(nameof(ComandoLimpiar), typeof(ICommand), typeof(ListToolbar), new PropertyMetadata(null));

    public static readonly DependencyProperty EstaCargandoProperty =
        DependencyProperty.Register(nameof(EstaCargando), typeof(bool), typeof(ListToolbar), new PropertyMetadata(false));

    public static readonly DependencyProperty TotalResultadosProperty =
        DependencyProperty.Register(
            nameof(TotalResultados),
            typeof(int),
            typeof(ListToolbar),
            new PropertyMetadata(0, AlCambiarTotal));

    public static readonly DependencyProperty TextoContadorProperty =
        DependencyProperty.Register(nameof(TextoContador), typeof(string), typeof(ListToolbar), new PropertyMetadata("0 resultados"));

    public static readonly DependencyProperty MostrarContadorProperty =
        DependencyProperty.Register(nameof(MostrarContador), typeof(bool), typeof(ListToolbar), new PropertyMetadata(true));

    public static readonly DependencyProperty HaySeleccionProperty =
        DependencyProperty.Register(nameof(HaySeleccion), typeof(bool), typeof(ListToolbar), new PropertyMetadata(false));

    public static readonly DependencyProperty AccionesDeSeleccionProperty =
        DependencyProperty.Register(nameof(AccionesDeSeleccion), typeof(object), typeof(ListToolbar), new PropertyMetadata(null));

    public ListToolbar()
    {
        InitializeComponent();
    }

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public ICommand? ComandoLimpiar
    {
        get => (ICommand?)GetValue(ComandoLimpiarProperty);
        set => SetValue(ComandoLimpiarProperty, value);
    }

    public bool EstaCargando
    {
        get => (bool)GetValue(EstaCargandoProperty);
        set => SetValue(EstaCargandoProperty, value);
    }

    public int TotalResultados
    {
        get => (int)GetValue(TotalResultadosProperty);
        set => SetValue(TotalResultadosProperty, value);
    }

    /// <summary>"1 resultado" o "N resultados" con separador de miles es-AR; lo calcula el control.</summary>
    public string TextoContador
    {
        get => (string)GetValue(TextoContadorProperty);
        private set => SetValue(TextoContadorProperty, value);
    }

    /// <summary>
    /// Muestra "N resultados" a la derecha de la barra. Una página que lo muestra en otro lugar (Artículos, en el pie junto
    /// al paginador) lo oculta y enlaza su texto a <see cref="TextoContador"/>, que se sigue calculando.
    /// </summary>
    public bool MostrarContador
    {
        get => (bool)GetValue(MostrarContadorProperty);
        set => SetValue(MostrarContadorProperty, value);
    }

    public bool HaySeleccion
    {
        get => (bool)GetValue(HaySeleccionProperty);
        set => SetValue(HaySeleccionProperty, value);
    }

    public object? AccionesDeSeleccion
    {
        get => GetValue(AccionesDeSeleccionProperty);
        set => SetValue(AccionesDeSeleccionProperty, value);
    }

    private static void AlCambiarTotal(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var barra = (ListToolbar)d;
        int total = (int)e.NewValue;
        barra.TextoContador = total == 1
            ? "1 resultado"
            : string.Create(CulturaArgentina, $"{total:N0} resultados");
    }
}
