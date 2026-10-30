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
        if ((bool)e.NewValue)
        {
            boton.Click += Boton_Click;
        }
    }

    private static void Boton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase { ContextMenu: { } menu } boton)
        {
            return;
        }

        if (menu.IsOpen)
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
