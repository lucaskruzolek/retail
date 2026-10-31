using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Retail.App.Helpers;

/// <summary>
/// Propiedad adjunta que abre el <see cref="ContextMenu"/> de un botón al hacer clic en él (por defecto un menú
/// contextual solo se abre con el botón derecho). Se usa en el botón "⋮" de las filas de las grillas: el menú agrupa las
/// acciones de la fila sin ocupar ancho en la columna.
/// <para>
/// El menú se despliega por debajo del botón con los bordes derechos alineados, y un segundo clic en el botón lo cierra.
/// </para>
/// <para>
/// Para que ese segundo clic llegue al botón, el menú se abre con <c>StaysOpen = true</c>: un menú que se cierra solo al
/// presionar afuera captura el mouse y, al presionar su propio botón, lo cierra y el botón lo vuelve a abrir. Como ya no
/// lo cierra WPF, lo cierra este helper: un clic en cualquier lugar de la ventana que no sea el botón, la rueda del
/// mouse, la pérdida de foco de la ventana y mover o cambiar su tamaño.
/// </para>
/// <para>
/// Aun así, dentro de una grilla el menú puede cerrarse antes del Click: al presionar el botón, la celda toma el foco y
/// el menú, que lo pierde, se cierra solo; al soltar, Click lo encontraba cerrado y lo volvía a abrir. Por eso Click no
/// decide por el estado del menú al soltar, sino por el que tenía al presionar el botón: si estaba abierto, el clic lo
/// cierra (o confirma el cierre) y no lo reabre. Decidirlo al presionar no depende de cuánto dure el clic ni de cuándo
/// llegue el evento Closed. Con el teclado (Espacio o Enter) no hay presión del mouse y se alterna según el estado actual.
/// </para>
/// </summary>
public static class MenuDesplegableHelper
{
    public static readonly DependencyProperty AbrirAlHacerClicProperty =
        DependencyProperty.RegisterAttached(
            "AbrirAlHacerClic",
            typeof(bool),
            typeof(MenuDesplegableHelper),
            new PropertyMetadata(false, AlCambiarAbrirAlHacerClic));

    private static readonly DependencyProperty SuscripcionProperty =
        DependencyProperty.RegisterAttached(
            "Suscripcion",
            typeof(Suscripcion),
            typeof(MenuDesplegableHelper),
            new PropertyMetadata(null));

    // Si el menú estaba abierto cuando se presionó el botón; null mientras no haya un clic del mouse en curso
    private static readonly DependencyProperty AbiertoAlPresionarProperty =
        DependencyProperty.RegisterAttached(
            "AbiertoAlPresionar",
            typeof(bool?),
            typeof(MenuDesplegableHelper),
            new PropertyMetadata(null));

    public static void SetAbrirAlHacerClic(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(AbrirAlHacerClicProperty, value);
    }

    public static bool GetAbrirAlHacerClic(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(AbrirAlHacerClicProperty);
    }

    /// <summary>
    /// Ubicación del menú: debajo del botón y con el borde derecho del menú sobre el borde derecho del botón.
    /// Es pública para poder probarla sin abrir un menú real.
    /// </summary>
    public static CustomPopupPlacement[] UbicarDebajoAlineadoALaDerecha(Size tamanoDelMenu, Size tamanoDelBoton, Point desplazamiento)
    {
        var punto = new Point((tamanoDelBoton.Width - tamanoDelMenu.Width) + desplazamiento.X, tamanoDelBoton.Height + desplazamiento.Y);
        return [new CustomPopupPlacement(punto, PopupPrimaryAxis.None)];
    }

    private static void AlCambiarAbrirAlHacerClic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase boton)
        {
            return;
        }

        boton.Click -= Boton_Click;
        boton.PreviewMouseLeftButtonDown -= Boton_PreviewMouseLeftButtonDown;
        boton.LostMouseCapture -= Boton_LostMouseCapture;
        if ((bool)e.NewValue)
        {
            boton.Click += Boton_Click;
            boton.PreviewMouseLeftButtonDown += Boton_PreviewMouseLeftButtonDown;
            boton.LostMouseCapture += Boton_LostMouseCapture;
        }
    }

    // Llega antes que cualquier otro manejo del clic (el foco de la celda que cierra el menú viene después)
    private static void Boton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ButtonBase { ContextMenu: { } menu } boton)
        {
            boton.SetValue(AbiertoAlPresionarProperty, menu.IsOpen);
        }
    }

    // El botón suelta la captura del mouse después de levantar Click (o al soltar afuera, sin Click): el clic terminó
    private static void Boton_LostMouseCapture(object sender, MouseEventArgs e)
    {
        ((DependencyObject)sender).ClearValue(AbiertoAlPresionarProperty);
    }

    private static void Boton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase { ContextMenu: { } menu } boton)
        {
            return;
        }

        bool estabaAbierto = (bool?)boton.GetValue(AbiertoAlPresionarProperty) ?? menu.IsOpen;
        boton.ClearValue(AbiertoAlPresionarProperty);
        if (estabaAbierto)
        {
            menu.IsOpen = false;
            return;
        }

        (boton.GetValue(SuscripcionProperty) as Suscripcion)?.Cancelar();

        menu.StaysOpen = true;
        menu.PlacementTarget = boton;
        menu.Placement = PlacementMode.Custom;
        menu.CustomPopupPlacementCallback = UbicarDebajoAlineadoALaDerecha;
        boton.SetValue(SuscripcionProperty, new Suscripcion(boton, menu));
        menu.IsOpen = true;
    }

    /// <summary>
    /// Sube por el árbol desde <paramref name="origen"/> buscando <paramref name="objetivo"/>. Los elementos que no son
    /// visuales (el texto de un Run) se resuelven por su padre lógico.
    /// </summary>
    internal static bool EstaDentroDe(DependencyObject? origen, DependencyObject objetivo)
    {
        var actual = origen;
        while (actual is not null)
        {
            if (ReferenceEquals(actual, objetivo))
            {
                return true;
            }

            actual = actual is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(actual)
                : LogicalTreeHelper.GetParent(actual);
        }

        return false;
    }

    /// <summary>Engancha un menú abierto a su ventana para cerrarlo ante cualquier interacción ajena, y se desengancha al cerrarse.</summary>
    private sealed class Suscripcion
    {
        private readonly ButtonBase _boton;
        private readonly ContextMenu _menu;
        private readonly Window? _ventana;

        public Suscripcion(ButtonBase boton, ContextMenu menu)
        {
            _boton = boton;
            _menu = menu;
            _ventana = Window.GetWindow(boton);

            _menu.Closed += AlCerrarseElMenu;
            if (_ventana is not null)
            {
                _ventana.PreviewMouseDown += AlPresionarEnLaVentana;
                _ventana.PreviewMouseWheel += AlGirarLaRueda;
                _ventana.Deactivated += AlPerderElFoco;
                _ventana.LocationChanged += AlMoverLaVentana;
                _ventana.SizeChanged += AlCambiarElTamano;
            }
        }

        public void Cancelar()
        {
            _menu.Closed -= AlCerrarseElMenu;
            if (_ventana is not null)
            {
                _ventana.PreviewMouseDown -= AlPresionarEnLaVentana;
                _ventana.PreviewMouseWheel -= AlGirarLaRueda;
                _ventana.Deactivated -= AlPerderElFoco;
                _ventana.LocationChanged -= AlMoverLaVentana;
                _ventana.SizeChanged -= AlCambiarElTamano;
            }
        }

        private void Cerrar()
        {
            _menu.IsOpen = false;
        }

        private void AlCerrarseElMenu(object? sender, RoutedEventArgs e)
        {
            Cancelar();
        }

        private void AlPresionarEnLaVentana(object sender, MouseButtonEventArgs e)
        {
            // El clic sobre el propio botón lo resuelve Click (alterna); cerrarlo acá haría que Click lo reabriera
            if (e.OriginalSource is DependencyObject origen && EstaDentroDe(origen, _boton))
            {
                return;
            }

            Cerrar();
        }

        private void AlGirarLaRueda(object sender, MouseWheelEventArgs e)
        {
            Cerrar();
        }

        private void AlPerderElFoco(object? sender, EventArgs e)
        {
            Cerrar();
        }

        private void AlMoverLaVentana(object? sender, EventArgs e)
        {
            Cerrar();
        }

        private void AlCambiarElTamano(object sender, SizeChangedEventArgs e)
        {
            Cerrar();
        }
    }
}
