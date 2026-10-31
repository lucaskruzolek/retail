using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Retail.App.Helpers;

/// <summary>
/// Propiedad adjunta que convierte un <see cref="ToggleButton"/> con un <see cref="ContextMenu"/> en un desplegable
/// (por defecto un menú contextual solo se abre con el botón derecho). Se usa en el botón "⋮" de las filas de las grillas
/// y en el "Más" de las acciones masivas: el menú agrupa acciones sin ocupar ancho en la pantalla.
/// <para>
/// Es el patrón clásico de WPF para un desplegable. Un <see cref="Button"/> común no sirve porque no tiene un estado
/// "presionado" persistente: no puede verse hundido mientras su menú está abierto. Un <see cref="ToggleButton"/> sí:
/// <c>IsChecked</c> es ese estado, y el estilo lo dibuja igual que un botón presionado.
/// </para>
/// <list type="bullet">
/// <item><description>Marcar el botón (<c>Checked</c>) configura el menú (debajo del botón y con los bordes derechos alineados,
/// <c>StaysOpen = false</c>) y lo abre. Con <c>StaysOpen = false</c> es WPF quien lo cierra al hacer clic afuera, al elegir
/// un ítem y con Esc (devolviendo el foco al botón): el helper no escucha eventos de la ventana.</description></item>
/// <item><description>Cuando el menú se cierra (<c>IsOpen</c> pasa a falso, sea por la causa que sea) el botón se desmarca. Y si el botón se
/// desmarca por otro camino (el teclado: Espacio o Enter sobre un botón marcado), se cierra el menú.</description></item>
/// <item><description>Mientras el menú está abierto el botón no recibe el mouse (<c>IsHitTestVisible = false</c>). Es la línea
/// clave: un menú con <c>StaysOpen = false</c> se cierra al presionar el mouse fuera de él, también sobre su propio botón; si
/// el botón recibiera ese mismo clic, lo alternaría y volvería a abrir el menú que acaba de cerrarse. Sin recibir el
/// mouse, el presionar lo consume el menú para cerrarse, el botón nunca ve el clic y queda en reposo.</description></item>
/// </list>
/// </summary>
public static class MenuDesplegableHelper
{
    public static readonly DependencyProperty AbrirAlHacerClicProperty =
        DependencyProperty.RegisterAttached(
            "AbrirAlHacerClic",
            typeof(bool),
            typeof(MenuDesplegableHelper),
            new PropertyMetadata(false, AlCambiarAbrirAlHacerClic));

    private static readonly DependencyPropertyDescriptor s_estadoDelMenu =
        DependencyPropertyDescriptor.FromProperty(ContextMenu.IsOpenProperty, typeof(ContextMenu));

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

    // Solo un ToggleButton puede mostrarse presionado mientras el menú está abierto; en cualquier otro elemento no hace nada
    private static void AlCambiarAbrirAlHacerClic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton boton)
        {
            return;
        }

        boton.Checked -= Boton_Checked;
        boton.Unchecked -= Boton_Unchecked;
        boton.ClearValue(ContextMenuService.IsEnabledProperty);
        if ((bool)e.NewValue)
        {
            boton.Checked += Boton_Checked;
            boton.Unchecked += Boton_Unchecked;

            // Sin esto el clic derecho abriría el menú por el camino nativo, sin marcar el botón ni protegerlo del
            // reabrir. El menú lo abre solo este helper (IsOpen = true no depende del servicio).
            ContextMenuService.SetIsEnabled(boton, false);
        }
    }

    private static void Boton_Checked(object sender, RoutedEventArgs e)
    {
        var boton = (ToggleButton)sender;
        if (boton.ContextMenu is not { } menu)
        {
            boton.IsChecked = false;
            return;
        }

        menu.PlacementTarget = boton;
        menu.Placement = PlacementMode.Custom;
        menu.CustomPopupPlacementCallback = UbicarDebajoAlineadoALaDerecha;
        menu.StaysOpen = false;

        // Restar antes de sumar evita duplicar la suscripción cuando el botón se marca varias veces
        s_estadoDelMenu.RemoveValueChanged(menu, Menu_AlCambiarIsOpen);
        s_estadoDelMenu.AddValueChanged(menu, Menu_AlCambiarIsOpen);
        menu.IsOpen = true;
    }

    private static void Boton_Unchecked(object sender, RoutedEventArgs e)
    {
        if (((ToggleButton)sender).ContextMenu is { IsOpen: true } menu)
        {
            menu.IsOpen = false;
        }
    }

    // Se observa IsOpen y no el evento Closed: WPF levanta Closed recién cuando el popup terminó de ocultarse, unos
    // instantes después, y en ese lapso el botón seguiría hundido y sin responder.
    private static void Menu_AlCambiarIsOpen(object? sender, EventArgs e)
    {
        var menu = (ContextMenu)sender!;
        if (menu.PlacementTarget is not ToggleButton boton)
        {
            return;
        }

        // Abierto: el botón deja de recibir el mouse, así el presionar sobre él lo consume el menú para cerrarse y no se reabre
        boton.IsHitTestVisible = !menu.IsOpen;
        if (!menu.IsOpen)
        {
            s_estadoDelMenu.RemoveValueChanged(menu, Menu_AlCambiarIsOpen);
            boton.IsChecked = false;
        }
    }
}
