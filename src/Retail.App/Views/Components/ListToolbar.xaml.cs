using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Barra superior de un listado: buscador y "Limpiar filtros" (la carga se indica en el encabezado de la página). La página
/// la enlaza a su ViewModel por propiedades de dependencia (el texto de búsqueda en ambos sentidos).
/// <para>
/// <see cref="AccionesDeSeleccion"/> (la <see cref="BulkActionBar"/> de la página) va siempre a la derecha: el badge de
/// cantidad aparece con filas marcadas y sus botones se habilitan solo entonces. Las acciones no tienen
/// padre lógico, así que heredan el DataContext por el árbol visual: el de la página, y sus comandos se enlazan directo.
/// </para>
/// </summary>
public partial class ListToolbar : UserControl
{
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

    public static readonly DependencyProperty AccionesDeSeleccionProperty =
        DependencyProperty.Register(nameof(AccionesDeSeleccion), typeof(object), typeof(ListToolbar), new PropertyMetadata(null));

    public static readonly DependencyProperty BuscadorHabilitadoProperty =
        DependencyProperty.Register(nameof(BuscadorHabilitado), typeof(bool), typeof(ListToolbar), new PropertyMetadata(true));

    public ListToolbar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// El cuadro de búsqueda, para las páginas que cuelgan de él un desplegable de resultados (el de Ingresos) o atienden
    /// sus teclas (flechas, Enter y Esc).
    /// </summary>
    public TextBox Buscador => CuadroBusqueda;

    /// <summary>Un buscador deshabilitado no recibe texto: la página lo apaga mientras le falta un dato previo.</summary>
    public bool BuscadorHabilitado
    {
        get => (bool)GetValue(BuscadorHabilitadoProperty);
        set => SetValue(BuscadorHabilitadoProperty, value);
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

    public object? AccionesDeSeleccion
    {
        get => GetValue(AccionesDeSeleccionProperty);
        set => SetValue(AccionesDeSeleccionProperty, value);
    }
}
