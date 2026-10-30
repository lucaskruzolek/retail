using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Retail.App.Views.Components;

/// <summary>
/// Barra de acciones masivas de un listado: muestra cuántas filas hay seleccionadas, las acciones que se pueden aplicar
/// a todas (su <see cref="ContentControl.Content"/>) y un botón para quitar la selección. La página la coloca en el
/// lugar de la barra del buscador mientras haya selección. Su plantilla vive en <c>Styles/Listados.xaml</c>.
/// </summary>
public class BulkActionBar : ContentControl
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    public static readonly DependencyProperty CantidadSeleccionadosProperty =
        DependencyProperty.Register(
            nameof(CantidadSeleccionados),
            typeof(int),
            typeof(BulkActionBar),
            new PropertyMetadata(0, AlCambiarCantidad));

    public static readonly DependencyProperty TextoSeleccionProperty =
        DependencyProperty.Register(nameof(TextoSeleccion), typeof(string), typeof(BulkActionBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ComandoCancelarProperty =
        DependencyProperty.Register(nameof(ComandoCancelar), typeof(ICommand), typeof(BulkActionBar), new PropertyMetadata(null));

    static BulkActionBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BulkActionBar), new FrameworkPropertyMetadata(typeof(BulkActionBar)));
    }

    public int CantidadSeleccionados
    {
        get => (int)GetValue(CantidadSeleccionadosProperty);
        set => SetValue(CantidadSeleccionadosProperty, value);
    }

    /// <summary>"1 seleccionado" o "N seleccionados"; lo calcula el control a partir de la cantidad.</summary>
    public string TextoSeleccion
    {
        get => (string)GetValue(TextoSeleccionProperty);
        private set => SetValue(TextoSeleccionProperty, value);
    }

    public ICommand? ComandoCancelar
    {
        get => (ICommand?)GetValue(ComandoCancelarProperty);
        set => SetValue(ComandoCancelarProperty, value);
    }

    private static void AlCambiarCantidad(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var barra = (BulkActionBar)d;
        int cantidad = (int)e.NewValue;
        barra.TextoSeleccion = cantidad == 1
            ? "1 seleccionado"
            : string.Create(CulturaArgentina, $"{cantidad:N0} seleccionados");
    }
}
