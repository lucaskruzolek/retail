using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Retail.App.Views.Dev;

/// <summary>
/// Estado de paginación de la galería de estilos: expone lo mismo que un ViewModel paginado (página actual, total,
/// tamaño y los comandos de navegación) para que <c>PaginationBar</c> funcione de verdad sin una base de datos detrás.
/// </summary>
public partial class PaginacionMuestra : ObservableObject
{
    private const int TotalRegistros = 248;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina), nameof(PuedeAvanzarPagina), nameof(InformacionPaginacion))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPaginas), nameof(PuedeAvanzarPagina), nameof(InformacionPaginacion))]
    private int _tamanoPagina = 20;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public int TotalPaginas => (int)Math.Ceiling(TotalRegistros / (double)TamanoPagina);

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion =>
        $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistros)} de {TotalRegistros}";

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
    }

    [RelayCommand]
    private void PrimeraPagina()
    {
        PaginaActual = 1;
    }

    [RelayCommand]
    private void PaginaAnterior()
    {
        PaginaActual = Math.Max(1, PaginaActual - 1);
    }

    [RelayCommand]
    private void PaginaSiguiente()
    {
        PaginaActual = Math.Min(TotalPaginas, PaginaActual + 1);
    }

    [RelayCommand]
    private void UltimaPagina()
    {
        PaginaActual = TotalPaginas;
    }

    [RelayCommand]
    private void IrAPagina(int pagina)
    {
        if (pagina >= 1 && pagina <= TotalPaginas)
        {
            PaginaActual = pagina;
        }
    }
}
