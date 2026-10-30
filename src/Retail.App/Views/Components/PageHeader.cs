using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Components;

/// <summary>
/// Encabezado de una página de trabajo: título, subtítulo y, a la derecha, las acciones de la página (su
/// <see cref="ContentControl.Content"/>). Va directo sobre el fondo, sin tarjeta. Su plantilla vive en
/// <c>Styles/Listados.xaml</c>. Es un control con contenido (y no un UserControl con una propiedad de tipo objeto) para
/// que los botones de las acciones hereden el DataContext de la página y sus bindings a comandos funcionen.
/// </summary>
public class PageHeader : ContentControl
{
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtituloProperty =
        DependencyProperty.Register(nameof(Subtitulo), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    static PageHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PageHeader), new FrameworkPropertyMetadata(typeof(PageHeader)));
    }

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    public string Subtitulo
    {
        get => (string)GetValue(SubtituloProperty);
        set => SetValue(SubtituloProperty, value);
    }
}
