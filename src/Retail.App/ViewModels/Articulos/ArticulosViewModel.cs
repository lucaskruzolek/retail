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
/// ViewModel principal para la administración del catálogo de artículos y visualización de alertas de stock (RF-04, RF-08).
/// </summary>
public partial class ArticulosViewModel : ObservableObject
{
    private readonly IInventarioService _inventarioService;
    private readonly IArticuloDialogService _dialogService;
    private readonly ILogger<ArticulosViewModel> _logger;

    private List<ArticuloDto> _cacheArticulos = new();
    private List<CategoriaDto> _cacheCategorias = new();
    private List<MarcaDto> _cacheMarcas = new();

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

    public bool HayArticuloSeleccionado => ArticuloSeleccionado != null;

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
        AplicarFiltrosLocales();
    }

    partial void OnIdCategoriaFiltroChanged(int value)
    {
        AplicarFiltrosLocales();
    }

    partial void OnSoloStockCriticoChanged(bool value)
    {
        AplicarFiltrosLocales();
    }

    [RelayCommand]
    public async Task CargarArticulosAsync()
    {
        try
        {
            IsBusy = true;
            MensajeError = null;
            MensajeEstado = "Cargando catálogo de artículos...";

            var articulos = await _inventarioService.ListarArticulosAsync();
            var categorias = await _inventarioService.ListarCategoriasAsync();
            var marcas = await _inventarioService.ListarMarcasAsync();

            _cacheArticulos = articulos.ToList();
            _cacheCategorias = categorias.ToList();
            _cacheMarcas = marcas.ToList();

            // Cargar categorías en el combo de filtro
            CategoriasFiltro.Clear();
            CategoriasFiltro.Add(new CategoriaDto { IdCategoria = 0, NombreCategoria = "Todas las Categorías" });
            foreach (var cat in _cacheCategorias)
            {
                CategoriasFiltro.Add(cat);
            }

            TotalArticulos = _cacheArticulos.Count;
            TotalAlertasStock = _cacheArticulos.Count(a => a.StockBajo);

            AplicarFiltrosLocales();

            MensajeEstado = $"Se cargaron {Articulos.Count} artículos ({TotalAlertasStock} con alerta de stock).";
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

    private void AplicarFiltrosLocales()
    {
        var query = _cacheArticulos.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var termino = TextoBusqueda.Trim();
            query = query.Where(a =>
                (!string.IsNullOrEmpty(a.CodigoBarras) && a.CodigoBarras.Contains(termino, StringComparison.OrdinalIgnoreCase)) ||
                a.Descripcion.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                (a.MarcaNombre != null && a.MarcaNombre.Contains(termino, StringComparison.OrdinalIgnoreCase)));
        }

        if (IdCategoriaFiltro > 0)
        {
            query = query.Where(a => a.IdCategoria == IdCategoriaFiltro);
        }

        if (SoloStockCritico)
        {
            query = query.Where(a => a.StockBajo);
        }

        Articulos.Clear();
        foreach (var item in query.OrderBy(a => a.Descripcion))
        {
            Articulos.Add(item);
        }

        MensajeEstado = $"Mostrando {Articulos.Count} de {TotalArticulos} artículos.";
    }
}
