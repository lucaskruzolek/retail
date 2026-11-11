using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Components;

/// <summary>Versiones del logo de Retail.</summary>
public enum VarianteLogo
{
    /// <summary>Símbolo y nombre en una línea: la versión principal.</summary>
    Horizontal,

    /// <summary>Símbolo arriba y nombre centrado debajo: espacios cuadrados o centrados, como el inicio de sesión.</summary>
    Vertical,

    /// <summary>Solo el símbolo de cuatro módulos, cuando el nombre ya está escrito al lado o no hay espacio.</summary>
    Simbolo,
}

/// <summary>
/// Logo de Retail dibujado con geometrías vectoriales, así se ve nítido a cualquier escala y sigue el tema activo.
/// <para>
/// Responsabilidades (plantilla implícita en <c>Styles/Controls.xaml</c>):
/// <list type="bullet">
/// <item><description>Los tres bloques y el nombre van en <c>TextPrimaryBrush</c> y el punto en <c>PrimaryBrush</c>; el
/// logo nunca define colores propios.</description></item>
/// <item><description>Las geometrías del símbolo (<c>GeometryLogoBloques</c>, <c>GeometryLogoPunto</c>) están en las dos
/// paletas: la oscura trae la compensación óptica (juntas de 4,8 en vez de 4), y el cambio de tema la aplica sola.</description></item>
/// <item><description>El tamaño se fija con <see cref="FrameworkElement.Height"/>; el ancho sale de la proporción de
/// cada variante.</description></item>
/// </list>
/// </para>
/// </summary>
public class LogoRetail : Control
{
    public static readonly DependencyProperty VarianteProperty =
        DependencyProperty.Register(nameof(Variante), typeof(VarianteLogo), typeof(LogoRetail), new PropertyMetadata(VarianteLogo.Horizontal));

    public VarianteLogo Variante
    {
        get => (VarianteLogo)GetValue(VarianteProperty);
        set => SetValue(VarianteProperty, value);
    }
}
