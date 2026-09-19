using System.Windows;

namespace Retail.App.Helpers;

/// <summary>
/// Provee propiedades de dependencia adjuntas para gestionar estados de carga
/// asíncrona (spinners) dentro de botones estándar en Retail.App.
/// </summary>
public static class ButtonHelper
{
    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.RegisterAttached(
            "IsLoading",
            typeof(bool),
            typeof(ButtonHelper),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty LoadingTextProperty =
        DependencyProperty.RegisterAttached(
            "LoadingText",
            typeof(string),
            typeof(ButtonHelper),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static void SetIsLoading(UIElement element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsLoadingProperty, value);
    }

    public static bool GetIsLoading(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsLoadingProperty);
    }

    public static void SetLoadingText(UIElement element, string? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(LoadingTextProperty, value);
    }

    public static string? GetLoadingText(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string?)element.GetValue(LoadingTextProperty);
    }
}
