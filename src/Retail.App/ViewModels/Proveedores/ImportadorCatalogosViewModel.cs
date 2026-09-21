using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el importador de planillas Excel/CSV de proveedores y curación del catálogo de artículos (Etapa 2.2 - Fase 2).
/// Orquesta el flujo de importación en streaming con MiniExcel, la vista previa del catálogo y la incorporación controlada a tienda.
/// </summary>
public partial class ImportadorCatalogosViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;
    private readonly ILogger<ImportadorCatalogosViewModel> _logger;

    // ------------------------------------------------------------------ //
    // Proveedor activo                                                    //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private ProveedorDto? _proveedorActivo;

    // ------------------------------------------------------------------ //
    // Estado del importador                                               //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private string? _rutaArchivo;

    [ObservableProperty]
    private string _columnaCodigo = "CODIGO";

    [ObservableProperty]
    private string _columnaDescripcion = "DESCRIPCION";

    [ObservableProperty]
    private string _columnaPrecio = "PRECIO";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _progresoImportacion;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private ResultadoImportacionDto? _ultimoResultado;

    // ------------------------------------------------------------------ //
    // Catálogo importado y filtros                                        //
    // ------------------------------------------------------------------ //

    public ObservableCollection<CatalogoProveedorDto> ItemsCatalogo { get; } = new();

    [ObservableProperty]
    private string _textoBusquedaCatalogo = string.Empty;

    [ObservableProperty]
    private EstadoVinculacionCatalogoEnum _filtroEstado = EstadoVinculacionCatalogoEnum.Todos;

    [ObservableProperty]
    private int _paginaActual = 1;

    [ObservableProperty]
    private int _totalItemsCatalogo;

    // ------------------------------------------------------------------ //
    // Incorporación a tienda                                              //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private decimal _porcentajeGananciaSugerido = 40m;

    [ObservableProperty]
    private int? _idCategoriaDestino;

    [ObservableProperty]
    private int? _idMarcaDestino;

    public bool HayArchivoSeleccionado => !string.IsNullOrWhiteSpace(RutaArchivo);

    public bool PuedeImportar =>
        HayArchivoSeleccionado &&
        ProveedorActivo != null &&
        !string.IsNullOrWhiteSpace(ColumnaCodigo) &&
        !string.IsNullOrWhiteSpace(ColumnaDescripcion) &&
        !string.IsNullOrWhiteSpace(ColumnaPrecio) &&
        !IsBusy;

    public IReadOnlyList<EstadoVinculacionCatalogoEnum> OpcionesEstado { get; } =
    [
        EstadoVinculacionCatalogoEnum.Todos,
        EstadoVinculacionCatalogoEnum.SinIncorporar,
        EstadoVinculacionCatalogoEnum.YaEnTienda,
    ];

    // ------------------------------------------------------------------ //
    // Constructor                                                         //
    // ------------------------------------------------------------------ //

    public ImportadorCatalogosViewModel(
        IProveedorService proveedorService,
        ILogger<ImportadorCatalogosViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ------------------------------------------------------------------ //
    // Propiedades encadenadas                                             //
    // ------------------------------------------------------------------ //

    partial void OnRutaArchivoChanged(string? value)
    {
        OnPropertyChanged(nameof(HayArchivoSeleccionado));
        OnPropertyChanged(nameof(PuedeImportar));
        ImportarPlanillaCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(PuedeImportar));
        ImportarPlanillaCommand.NotifyCanExecuteChanged();
    }

    partial void OnFiltroEstadoChanged(EstadoVinculacionCatalogoEnum value)
    {
        _ = CargarCatalogoAsync();
    }

    partial void OnTextoBusquedaCatalogoChanged(string value)
    {
        _ = CargarCatalogoAsync();
    }

    // ------------------------------------------------------------------ //
    // Comandos                                                            //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private void ExaminarArchivo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar planilla de proveedor",
            Filter = "Planillas Excel y CSV|*.xlsx;*.xls;*.csv|Excel (*.xlsx;*.xls)|*.xlsx;*.xls|CSV (*.csv)|*.csv",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            RutaArchivo = dialog.FileName;
            MensajeEstado = $"Archivo seleccionado: {System.IO.Path.GetFileName(RutaArchivo)}";
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeImportar))]
    private async Task ImportarPlanillaAsync(CancellationToken cancellationToken = default)
    {
        if (ProveedorActivo is null || string.IsNullOrWhiteSpace(RutaArchivo)) return;

        IsBusy = true;
        ProgresoImportacion = 0;
        MensajeError = null;
        UltimoResultado = null;

        try
        {
            var mapeo = new MapeoColumnasDto
            {
                IdProveedor = ProveedorActivo.IdProveedor,
                ColumnaCodigo = ColumnaCodigo.Trim(),
                ColumnaDescripcion = ColumnaDescripcion.Trim(),
                ColumnaPrecioCosto = ColumnaPrecio.Trim()
            };

            var progreso = new Progress<int>(pct =>
            {
                ProgresoImportacion = pct;
            });

            await using var stream = System.IO.File.OpenRead(RutaArchivo);
            var resultado = await Task.Run(
                () => _proveedorService.ImportarPlanillaProveedorAsync(stream, mapeo, progreso, cancellationToken),
                cancellationToken);

            UltimoResultado = resultado;
            MensajeEstado = $"Importación completada: {resultado.NuevosRegistros} nuevos, {resultado.PreciosActualizados} actualizados, {resultado.FilasConError} errores.";
            await CargarCatalogoAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            MensajeEstado = "Importación cancelada.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante importación de planilla del proveedor {IdProveedor}", ProveedorActivo?.IdProveedor);
            MensajeError = $"Error al importar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgresoImportacion = 100;
        }
    }

    [RelayCommand]
    public async Task CargarCatalogoAsync(CancellationToken cancellationToken = default)
    {
        if (ProveedorActivo is null) return;

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
                TamañoPagina = 50
            };

            var items = await Task.Run(
                () => _proveedorService.ListarItemsCatalogoAsync(consulta, cancellationToken),
                cancellationToken);

            ItemsCatalogo.Clear();
            foreach (var item in items)
            {
                ItemsCatalogo.Add(item);
            }

            TotalItemsCatalogo = ItemsCatalogo.Count;
        }
        catch (OperationCanceledException) { }
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
    private async Task IncorporarSeleccionadosAsync(IList<CatalogoProveedorDto>? itemsSeleccionados)
    {
        if (itemsSeleccionados is null || itemsSeleccionados.Count == 0 || ProveedorActivo is null)
        {
            MensajeError = "Seleccione al menos un artículo del catálogo para incorporar.";
            return;
        }

        if (!IdCategoriaDestino.HasValue)
        {
            MensajeError = "Debe seleccionar una categoría de destino.";
            return;
        }

        IsBusy = true;
        MensajeError = null;

        try
        {
            var dto = new IncorporarCatalogoArticulosDto
            {
                IdsCatalogo = itemsSeleccionados.Select(i => i.Id).ToList(),
                IdCategoria = IdCategoriaDestino ?? 0, // <-- Solución: extraemos el int seguro de su contenedor nullable
                IdMarca = IdMarcaDestino ?? 0,
                PorcentajeGananciaSugerido = PorcentajeGananciaSugerido
            };

            await Task.Run(() => _proveedorService.IncorporarArticulosATiendaAsync(dto));
            MensajeEstado = $"{itemsSeleccionados.Count} artículo(s) incorporados a la tienda correctamente.";
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
}
