using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MahApps.Metro.IconPacks;
using Wpf.Ui.Controls;

namespace Retail.App.Views.Components;

/// <summary>
/// Ícono de BoxIcons para los lugares donde WPF-UI pide un <see cref="IconElement"/> (por ejemplo el <c>Icon</c> de un
/// <c>ui:TextBox</c>). Reemplaza a <c>ui:ImageIcon</c> con <c>{iconPacks:BoxIconsImage ... Brush=...}</c>: esa extensión
/// dibuja el ícono con un pincel fijo, tomado en el momento de crear la vista, y no cambia al pasar de tema claro a
/// oscuro. Acá el color es el <c>Foreground</c> del elemento, que sí admite <c>{DynamicResource}</c>.
/// </summary>
public class IconoBoxIcons : IconElement
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(PackIconBoxIconsKind),
            typeof(IconoBoxIcons),
            new PropertyMetadata(default(PackIconBoxIconsKind), AlCambiarElIcono));

    private PackIconBoxIcons? _icono;

    public PackIconBoxIconsKind Kind
    {
        get => (PackIconBoxIconsKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override UIElement InitializeChildren()
    {
        _icono = new PackIconBoxIcons { Kind = Kind };

        // El pincel del ícono sigue al Foreground de este elemento, que se enlaza a un recurso dinámico
        BindingOperations.SetBinding(_icono, Control.ForegroundProperty, new Binding(nameof(Foreground)) { Source = this });

        return _icono;
    }

    private static void AlCambiarElIcono(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var elemento = (IconoBoxIcons)d;

        if (elemento._icono is not null)
        {
            elemento._icono.Kind = (PackIconBoxIconsKind)e.NewValue;
        }
    }
}
