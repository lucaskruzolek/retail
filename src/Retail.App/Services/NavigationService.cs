using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Retail.App.Services;

/// <summary>
/// Implementación Singleton del servicio de navegación sobre el Frame principal de MainWindow.
/// Cada pantalla se resuelve dentro de su propio scope de inyección de dependencias, así que tiene su
/// propio DbContext: dos pantallas nunca comparten contexto, y al salir de una se libera el suyo junto
/// con las entidades que había rastreado (H-19, RNF-03).
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Frame? _frame;
    private IServiceScope? _scopePantallaActual;

    public event Action<Type>? Navigated;

    public NavigationService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));

        // La app no vuelve atrás: el historial solo retendría pantallas cuyo scope ya se descartó.
        _frame.Navigated += (_, _) => VaciarHistorial();
    }

    public void NavigateTo<TView>() where TView : FrameworkElement
    {
        NavigateTo(typeof(TView));
    }

    public void NavigateTo<TView>(Action<TView> configure) where TView : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(configure);

        Navegar(typeof(TView), view => configure((TView)view));
    }

    public void NavigateTo(Type viewType)
    {
        ArgumentNullException.ThrowIfNull(viewType);

        Navegar(viewType, configure: null);
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void GoBack()
    {
        if (CanGoBack)
        {
            _frame?.GoBack();
        }
    }

    private void Navegar(Type viewType, Action<object>? configure)
    {
        if (_frame == null)
        {
            throw new InvalidOperationException("El servicio de navegación no ha sido inicializado con un Frame contenedor.");
        }

        var scopeNuevo = _scopeFactory.CreateScope();
        object view;
        try
        {
            view = scopeNuevo.ServiceProvider.GetRequiredService(viewType);
            configure?.Invoke(view);
        }
        catch
        {
            scopeNuevo.Dispose();
            throw;
        }

        _frame.Navigate(view);

        // Se descarta la pantalla anterior: su DbContext y sus ViewModels descartables (que cancelan sus cargas).
        var scopeAnterior = _scopePantallaActual;
        _scopePantallaActual = scopeNuevo;
        scopeAnterior?.Dispose();

        Navigated?.Invoke(viewType);
    }

    private void VaciarHistorial()
    {
        while (_frame?.CanGoBack == true)
        {
            _frame.RemoveBackEntry();
        }
    }
}
