using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Retail.App.Services;

/// <summary>
/// Implementación Singleton del servicio de navegación sobre el Frame principal de MainWindow.
/// Resuelve las vistas desde el contenedor de inyección de dependencias para preservar ciclos de vida.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private Frame? _frame;

    public event Action<Type>? Navigated;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public void NavigateTo<TView>() where TView : FrameworkElement
    {
        NavigateTo(typeof(TView));
    }

    public void NavigateTo(Type viewType)
    {
        ArgumentNullException.ThrowIfNull(viewType);

        if (_frame == null)
        {
            throw new InvalidOperationException("El servicio de navegación no ha sido inicializado con un Frame contenedor.");
        }

        var view = _serviceProvider.GetRequiredService(viewType);
        _frame.Navigate(view);
        Navigated?.Invoke(viewType);
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void GoBack()
    {
        if (CanGoBack)
        {
            _frame?.GoBack();
        }
    }
}
