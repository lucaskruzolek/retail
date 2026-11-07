using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Components;

/// <summary>
/// Encabezado de una página de trabajo: título, un ícono de información con la descripción de la página (tooltip) y, a la derecha, las acciones de la página (su
/// <see cref="ContentControl.Content"/>). Va directo sobre el fondo, sin tarjeta. Su plantilla vive en
/// <c>Styles/Listados.xaml</c>. Es un control con contenido (y no un UserControl con una propiedad de tipo objeto) para
/// que los botones de las acciones hereden el DataContext de la página y sus bindings a comandos funcionen.
/// </summary>
public class PageHeader : ContentControl
{
    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescripcionProperty =
        DependencyProperty.Register(nameof(Descripcion), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EstaCargandoProperty =
        DependencyProperty.Register(nameof(EstaCargando), typeof(bool), typeof(PageHeader), new PropertyMetadata(false));

    static PageHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PageHeader), new FrameworkPropertyMetadata(typeof(PageHeader)));
    }

    public string Titulo
    {
        get => (string)GetValue(TituloProperty);
        set => SetValue(TituloProperty, value);
    }

    /// <summary>
    /// Qué muestra la página activa, para el tooltip del ícono. Conviene que empiece con el nombre de la página: el título
    /// del encabezado es el de la sección y el selector cambia de página sin cambiarlo.
    /// </summary>
    public string Descripcion
    {
        get => (string)GetValue(DescripcionProperty);
        set => SetValue(DescripcionProperty, value);
    }

    /// <summary>
    /// La página está cargando o consultando: gira el <see cref="IndicadorCarga"/> junto al título. Su lugar está reservado,
    /// así que no corre nada al aparecer.
    /// </summary>
    public bool EstaCargando
    {
        get => (bool)GetValue(EstaCargandoProperty);
        set => SetValue(EstaCargandoProperty, value);
    }
}
