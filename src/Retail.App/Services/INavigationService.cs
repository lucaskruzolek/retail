using System.Windows;
using System.Windows.Controls;

namespace Retail.App.Services;

/// <summary>
/// Contrato para el servicio desacoplado de navegación entre páginas del Shell de Retail POS.
/// Permite navegar resolviendo vistas a través del contenedor IoC sin acoplar las páginas entre sí.
/// </summary>
public interface INavigationService
{
    void Initialize(Frame frame);

    void NavigateTo<TView>() where TView : FrameworkElement;

    void NavigateTo<TView>(Action<TView> configure) where TView : FrameworkElement;

    void NavigateTo(Type viewType);

    bool CanGoBack { get; }

    void GoBack();

    event Action<Type>? Navigated;
}
