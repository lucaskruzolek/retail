using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Views.Components;

/// <summary>
/// Ícono de información con la descripción en un tooltip: reemplaza a los subtítulos y leyendas de una página, que de otro
/// modo ocuparían una línea (regla R11 del sistema de diseño). Es chico (12) pero su zona de impacto es un cuadrado de 20
/// que cubre todo el glifo, y el tooltip aparece a los 150 ms. Sin <see cref="Texto"/> se oculta.
/// </summary>
public partial class IconoInformacion : UserControl
{
    public static readonly DependencyProperty TextoProperty =
        DependencyProperty.Register(nameof(Texto), typeof(string), typeof(IconoInformacion), new PropertyMetadata(string.Empty));

    public IconoInformacion()
    {
        InitializeComponent();
    }

    /// <summary>La descripción que muestra el tooltip.</summary>
    public string? Texto
    {
        get => (string?)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }
}
