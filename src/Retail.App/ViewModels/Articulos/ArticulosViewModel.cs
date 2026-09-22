using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel principal para la administración del catálogo de artículos y visualización de alertas de stock con push-down a SQL Server (RF-04, RF-08, Ley 8).
/// </summary>
public partial class ArticulosViewModel : ObservableObject
{
    private readonly IInventarioService _inventarioService;
    private readonly IArticuloDialogService _dialogService;
    private readonly ILogger<ArticulosViewModel> _logger;

    private List<CategoriaDto> _cacheCategorias = new();
    private List<MarcaDto> _cacheMarcas = new();
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<ArticuloDto> Articulos { get; } = new();

    public ObservableCollection<CategoriaDto> CategoriasFiltro { get; } = new();

    [ObservableProperty]
    private ArticuloDto? _articuloSeleccionado;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private int _idCategoriaFiltro;

    [ObservableProperty]
    private bool _soloStockCritico;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private int _totalArticulos;

    [ObservableProperty]
    private int _totalAlertasStock;

    [ObservableProperty]
    private int _paginaActual = 1;

    [ObservableProperty]
    private int _tamanoPagina = 10;

    [ObservableProperty]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private int _totalRegistrosFiltrados;

    public bool HayArticuloSeleccionado => ArticuloSeleccionado != null;

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalRegistrosFiltrados == 0
        ? "Sin artículos registrados"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistrosFiltrados)} de {TotalRegistrosFiltrados} artículos";

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public ArticulosViewModel(
        IInventarioService inventarioService,
        IArticuloDialogService dialogService,
        ILogger<ArticulosViewModel>? logger = null)
    {
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? NullLogger<ArticulosViewModel>.Instance;
    }

    partial void OnArticuloSeleccionadoChanged(ArticuloDto? value)
    {
        OnPropertyChanged(nameof(HayArticuloSeleccionado));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        PaginaActual = 1;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = DebounceBusquedaAsync(token);
    }

    private async Task DebounceBusquedaAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(250, token);
            await CargarArticulosAsync(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (token.IsCancellationRequested ||
                                   ex.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
        { }
    }

    partial void OnIdCategoriaFiltroChanged(int value)
    {
        PaginaActual = 1;
        _ = CargarArticulosAsync();
    }

    partial void OnSoloStockCriticoChanged(bool value)
    {
        PaginaActual = 1;
        _ = CargarArticulosAsync();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        _ = CargarArticulosAsync();
    }

    [RelayCommand]
    public async Task CargarArticulosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            MensajeError = null;
            MensajeEstado = "Cargando catálogo de artículos...";

            if (CategoriasFiltro.Count == 0)
            {
                var categorias = await _inventarioService.ListarCategoriasAsync(cancellationToken);
                var marcas = await _inventarioService.ListarMarcasAsync(cancellationToken);
                _cacheCategorias = categorias.ToList();
                _cacheMarcas = marcas.ToList();

                EjecutarEnDispatcher(() =>
                {
                    CategoriasFiltro.Clear();
                    CategoriasFiltro.Add(new CategoriaDto { IdCategoria = 0, NombreCategoria = "Todas las Categorías" });
                    foreach (var cat in _cacheCategorias)
                    {
                        CategoriasFiltro.Add(cat);
                    }
                });
            }

            var consulta = new ConsultaArticulosDto
            {
                TerminoBusqueda = TextoBusqueda,
                IdCategoria = IdCategoriaFiltro > 0 ? IdCategoriaFiltro : null,
                SoloStockCritico = SoloStockCritico,
                Pagina = PaginaActual,
                TamanoPagina = TamanoPagina
            };

            var resultado = await Task.Run(
                () => _inventarioService.ListarArticulosPaginadosAsync(consulta, cancellationToken),
                cancellationToken);

            EjecutarEnDispatcher(() =>
            {
                Articulos.Clear();
                foreach (var art in resultado.Items)
                {
                    Articulos.Add(art);
                }
            });

            TotalArticulos = resultado.TotalArticulos;
            TotalAlertasStock = resultado.TotalAlertasStock;
            TotalRegistrosFiltrados = resultado.TotalRegistros;
            TotalPaginas = resultado.TotalPaginas;

            OnPropertyChanged(nameof(PuedeRetrocederPagina));
            OnPropertyChanged(nameof(PuedeAvanzarPagina));
            OnPropertyChanged(nameof(InformacionPaginacion));

            MensajeEstado = $"Mostrando {Articulos.Count} de {TotalArticulos} artículos ({TotalAlertasStock} con alerta de stock).";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested ||
                                   ex.InnerException is OperationCanceledException ||
                                   ex.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Consulta de artículos cancelada por nueva acción del usuario.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar el catálogo de artículos: {Mensaje}", ex.Message);
            MensajeError = $"Error al cargar catálogo: {ex.Message}";
            _dialogService.MostrarError("Error de Carga", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task NuevoArticuloAsync()
    {
        try
        {
            if (_cacheCategorias.Count == 0 || _cacheMarcas.Count == 0)
            {
                _cacheCategorias = (await _inventarioService.ListarCategoriasAsync()).ToList();
                _cacheMarcas = (await _inventarioService.ListarMarcasAsync()).ToList();
            }

            var nuevoDto = _dialogService.MostrarDialogoCrear(
                _cacheCategorias,
                _cacheMarcas,
                async (crearDto) =>
                {
                    await _inventarioService.CrearArticuloAsync(crearDto);
                });

            if (nuevoDto != null)
            {
                _dialogService.MostrarInformacion("Catálogo de Artículos", "El artículo fue incorporado al catálogo exitosamente.");
                await CargarArticulosAsync();
            }
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Validación de negocio al crear artículo: {Mensaje}", dex.Message);
            _dialogService.MostrarError("Regla de Catálogo", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear artículo: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error Inesperado", ex.Message);
        }
    }

    [RelayCommand]
    public async Task EditarArticuloAsync(ArticuloDto? param)
    {
        var target = param ?? ArticuloSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarInformacion("Catálogo", "Seleccione un artículo de la grilla para modificar.");
            return;
        }

        try
        {
            if (_cacheCategorias.Count == 0 || _cacheMarcas.Count == 0)
            {
                _cacheCategorias = (await _inventarioService.ListarCategoriasAsync()).ToList();
                _cacheMarcas = (await _inventarioService.ListarMarcasAsync()).ToList();
            }

            var modificadoDto = _dialogService.MostrarDialogoModificar(
                target,
                _cacheCategorias,
                _cacheMarcas,
                async (actDto) =>
                {
                    await _inventarioService.ActualizarArticuloAsync(actDto);
                });

            if (modificadoDto != null)
            {
                _dialogService.MostrarInformacion("Catálogo de Artículos", "El artículo fue actualizado exitosamente.");
                await CargarArticulosAsync();
            }
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Validación de negocio al modificar artículo: {Mensaje}", dex.Message);
            _dialogService.MostrarError("Regla de Catálogo", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al modificar artículo: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error Inesperado", ex.Message);
        }
    }

    [RelayCommand]
    public async Task BajaArticuloAsync(ArticuloDto? param)
    {
        var target = param ?? ArticuloSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarInformacion("Catálogo", "Seleccione un artículo de la grilla para dar de baja.");
            return;
        }

        var confirmacion = _dialogService.Confirmar(
            "Confirmar Baja de Artículo",
            $"¿Está seguro de dar de baja el artículo '{target.Descripcion}'?\nEl registro será archivado lógicamente (Soft Delete).");

        if (!confirmacion)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _inventarioService.BajaArticuloAsync(target.IdArticulo);
            _dialogService.MostrarInformacion("Baja Exitosa", $"El artículo '{target.Descripcion}' fue dado de baja correctamente.");
            await CargarArticulosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al dar de baja el artículo {Id}: {Mensaje}", target.IdArticulo, ex.Message);
            _dialogService.MostrarError("Error de Baja", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void AlternarSoloStockCritico()
    {
        SoloStockCritico = !SoloStockCritico;
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarArticulosAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarArticulosAsync();
        }
    }

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarArticulosAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarArticulosAsync();
        }
    }

    private static void EjecutarEnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
