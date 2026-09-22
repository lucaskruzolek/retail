using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.App.Views.Dialogs;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para la consulta y curaduría del catálogo de distribuidores e incorporación asistida a tienda (Etapa 2.2).
/// Orquesta la vista previa del catálogo con push-down a SQL, filtros reactivos y la incorporación controlada a tienda.
/// </summary>
public partial class ImportadorCatalogosViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;
    private readonly IProveedorDialogService _dialogService;
    private readonly IInventarioService _inventarioService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<ImportadorCatalogosViewModel> _logger;

    // ------------------------------------------------------------------ //
    // Proveedor activo y selección                                       //
    // ------------------------------------------------------------------ //

    public ObservableCollection<ProveedorDto> ProveedoresDisponibles { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneProveedorActivo))]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private ProveedorDto? _proveedorActivo;

    public bool TieneProveedorActivo => ProveedorActivo != null;

    // ------------------------------------------------------------------ //
    // Estado del importador y progreso                                   //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstado))]
    private string? _mensajeEstado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeEstado => !string.IsNullOrWhiteSpace(MensajeEstado);
    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    [ObservableProperty]
    private ResultadoImportacionDto? _ultimoResultado;

    [ObservableProperty]
    private bool _tieneItemsSeleccionados;

    [ObservableProperty]
    private bool? _seleccionarTodos = false;

    private bool _isUpdatingSeleccionarTodos;

    public bool PuedeImportar => TieneProveedorActivo && !IsBusy;

    // ------------------------------------------------------------------ //
    // Catálogo importado, filtros y paginación                            //
    // ------------------------------------------------------------------ //

    public ObservableCollection<CatalogoProveedorDto> ItemsCatalogo { get; } = new();

    public IReadOnlyList<EstadoVinculacionCatalogoEnum> OpcionesEstado { get; } = Enum.GetValues<EstadoVinculacionCatalogoEnum>();

    [ObservableProperty]
    private string _textoBusquedaCatalogo = string.Empty;

    [ObservableProperty]
    private EstadoVinculacionCatalogoEnum _filtroEstado = EstadoVinculacionCatalogoEnum.Todos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _totalItemsCatalogo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _totalPaginas = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _tamanoPagina = 50;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = new[] { 20, 50, 100 };

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalItemsCatalogo == 0
        ? "Sin artículos"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalItemsCatalogo)} de {TotalItemsCatalogo} artículos";

    public ImportadorCatalogosViewModel(
        IProveedorService proveedorService,
        IProveedorDialogService dialogService,
        IInventarioService inventarioService,
        INavigationService navigationService,
        ILogger<ImportadorCatalogosViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CargarProveedoresDisponiblesAsync(int? idProveedorSeleccionar = null)
    {
        try
        {
            var proveedores = await Task.Run(() => _proveedorService.ListarProveedoresAsync());
            ProveedoresDisponibles.Clear();
            foreach (var p in proveedores)
            {
                ProveedoresDisponibles.Add(p);
            }

            if (idProveedorSeleccionar.HasValue)
            {
                ProveedorActivo = ProveedoresDisponibles.FirstOrDefault(p => p.IdProveedor == idProveedorSeleccionar.Value);
            }
            else if (ProveedorActivo == null && ProveedoresDisponibles.Count > 0)
            {
                ProveedorActivo = ProveedoresDisponibles[0];
            }
            else if (ProveedorActivo != null)
            {
                ProveedorActivo = ProveedoresDisponibles.FirstOrDefault(p => p.IdProveedor == ProveedorActivo.IdProveedor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar la lista de proveedores disponibles");
            MensajeError = "No se pudo cargar la lista de proveedores.";
        }
    }

    public async Task InicializarAsync(ProveedorDto? proveedor = null)
    {
        await CargarProveedoresDisponiblesAsync(proveedor?.IdProveedor);
        if (proveedor != null)
        {
            if (!ProveedoresDisponibles.Any(p => p.IdProveedor == proveedor.IdProveedor))
            {
                ProveedoresDisponibles.Add(proveedor);
            }
            ProveedorActivo = proveedor;
        }

        PaginaActual = 1;
        if (ProveedorActivo != null)
        {
            await CargarCatalogoAsync();
        }
    }

    public void Inicializar(ProveedorDto? proveedor = null)
    {
        _ = InicializarAsync(proveedor);
    }

    partial void OnProveedorActivoChanged(ProveedorDto? value)
    {
        PaginaActual = 1;
        LimpiarItemsCatalogo();
        TotalItemsCatalogo = 0;
        TotalPaginas = 1;
        MensajeError = null;
        MensajeEstado = null;
        UltimoResultado = null;
        TieneItemsSeleccionados = false;
        SeleccionarTodos = false;

        if (value != null)
        {
            _ = CargarCatalogoAsync();
        }
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        _ = CargarCatalogoAsync();
    }

    partial void OnFiltroEstadoChanged(EstadoVinculacionCatalogoEnum value)
    {
        PaginaActual = 1;
        _ = CargarCatalogoAsync();
    }

    partial void OnTextoBusquedaCatalogoChanged(string value)
    {
        PaginaActual = 1;
        _ = CargarCatalogoAsync();
    }

    // ------------------------------------------------------------------ //
    // Comandos de Importación                                            //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ImportarPlanillaAsync()
    {
        if (ProveedorActivo is null)
        {
            return;
        }

        var resultado = await _dialogService.AbrirImportarPlanillaAsync(ProveedorActivo);
        if (resultado != null)
        {
            UltimoResultado = resultado;
            MensajeEstado = $"Importación completada: {resultado.NuevosRegistros} nuevos, {resultado.PreciosActualizados} precios actualizados, {resultado.FilasConError} errores.";
            PaginaActual = 1;
            await CargarCatalogoAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Explorador de Catálogo y Paginación en Servidor                    //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    public async Task CargarCatalogoAsync(CancellationToken cancellationToken = default)
    {
        if (ProveedorActivo is null)
        {
            return;
        }

        IsBusy = true;
        MensajeError = null;

        try
        {
            var consulta = new ConsultaCatalogoProveedorDto
            {
                IdProveedor = ProveedorActivo.IdProveedor,
                TerminoBusqueda = TextoBusquedaCatalogo,
                EstadoVinculacion = FiltroEstado,
                Pagina = PaginaActual,
                TamañoPagina = TamanoPagina
            };

            var resultado = await Task.Run(
                () => _proveedorService.ListarItemsCatalogoAsync(consulta, cancellationToken),
                cancellationToken);

            LimpiarItemsCatalogo();
            foreach (var item in resultado.Items)
            {
                item.PropertyChanged += Item_PropertyChanged;
                ItemsCatalogo.Add(item);
            }

            TotalItemsCatalogo = resultado.TotalRegistros;
            TotalPaginas = Math.Max(1, resultado.TotalPaginas);

            _isUpdatingSeleccionarTodos = true;
            try
            {
                SeleccionarTodos = false;
                TieneItemsSeleccionados = false;
            }
            finally
            {
                _isUpdatingSeleccionarTodos = false;
            }

            OnPropertyChanged(nameof(PuedeRetrocederPagina));
            OnPropertyChanged(nameof(PuedeAvanzarPagina));
            OnPropertyChanged(nameof(InformacionPaginacion));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested ||
                                   ex.InnerException is OperationCanceledException ||
                                   ex.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Carga de catálogo cancelada por nueva acción del usuario.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar catálogo del proveedor");
            MensajeError = "No se pudo cargar el catálogo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarCatalogoAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Curaduría y Vinculación Asistida (RF-05)                           //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task VincularArticuloAsync(CatalogoProveedorDto? item)
    {
        if (item is null)
        {
            return;
        }

        var vinculado = await _dialogService.AbrirSelectorVinculacionArticuloAsync(item);
        if (vinculado)
        {
            MensajeEstado = $"Artículo vinculado con éxito al código {item.CodigoProveedor}.";
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    private async Task IncorporarSeleccionadosAsync(IList<CatalogoProveedorDto>? itemsSeleccionadosParam)
    {
        var itemsSeleccionados = (itemsSeleccionadosParam != null && itemsSeleccionadosParam.Count > 0)
            ? itemsSeleccionadosParam
            : ItemsCatalogo.Where(i => i.EstaSeleccionado).ToList();

        if (itemsSeleccionados is null || itemsSeleccionados.Count == 0 || ProveedorActivo is null)
        {
            MensajeError = "Seleccione al menos un artículo del catálogo para incorporar.";
            return;
        }

        try
        {
            var categorias = await _inventarioService.ListarCategoriasAsync();
            var marcas = await _inventarioService.ListarMarcasAsync();

            var modalVm = new IncorporarArticulosModalViewModel();
            modalVm.Inicializar(categorias, marcas, itemsSeleccionados);

            var dialog = new IncorporarArticulosModalDialog
            {
                DataContext = modalVm,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var dialogRes = dialog.ShowDialog();
            if (dialogRes != true || !modalVm.DialogResult)
            {
                return;
            }

            IsBusy = true;
            MensajeError = null;

            var dto = modalVm.ObtenerDto();

            await Task.Run(() => _proveedorService.IncorporarArticulosATiendaAsync(dto));
            MensajeEstado = $"{itemsSeleccionados.Count} artículo(s) incorporados a la tienda correctamente.";
            TieneItemsSeleccionados = false;
            SeleccionarTodos = false;
            await CargarCatalogoAsync();
        }
        catch (DomainException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al incorporar artículos a tienda");
            MensajeError = "Error al incorporar artículos.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSeleccionarTodosChanged(bool? value)
    {
        if (_isUpdatingSeleccionarTodos || value is null)
        {
            return;
        }

        _isUpdatingSeleccionarTodos = true;
        try
        {
            bool seleccionar = value.Value;
            foreach (var item in ItemsCatalogo)
            {
                item.EstaSeleccionado = seleccionar;
            }
            TieneItemsSeleccionados = ItemsCatalogo.Any(i => i.EstaSeleccionado);
        }
        finally
        {
            _isUpdatingSeleccionarTodos = false;
        }
    }

    private void LimpiarItemsCatalogo()
    {
        foreach (var item in ItemsCatalogo)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }
        ItemsCatalogo.Clear();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CatalogoProveedorDto.EstaSeleccionado))
        {
            ActualizarEstadoSeleccion();
        }
    }

    private void ActualizarEstadoSeleccion()
    {
        var total = ItemsCatalogo.Count;
        var seleccionados = ItemsCatalogo.Count(i => i.EstaSeleccionado);
        TieneItemsSeleccionados = seleccionados > 0;

        if (!_isUpdatingSeleccionarTodos)
        {
            _isUpdatingSeleccionarTodos = true;
            try
            {
                SeleccionarTodos = total == 0 ? false : (seleccionados == total ? true : (seleccionados == 0 ? false : null));
            }
            finally
            {
                _isUpdatingSeleccionarTodos = false;
            }
        }
    }
}
