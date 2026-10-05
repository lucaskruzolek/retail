using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el diálogo modal de importación de planillas de catálogos mayoristas en streaming (Etapa 2.2).
/// Encapsula la selección de archivo, configuración de columnas y feedback de progreso de MiniExcel.
/// </summary>
public partial class ImportarPlanillaViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;
    private readonly ILogger<ImportarPlanillaViewModel> _logger;

    [ObservableProperty]
    private ProveedorDto? _proveedor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayArchivoSeleccionado))]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private string? _rutaArchivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private string _columnaCodigo = "CODIGO";

    [ObservableProperty]
    private string? _columnaCodigoBarras = "EAN";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private string _columnaDescripcion = "DESCRIPCION";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private string _columnaPrecio = "PRECIO";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private int _filaEncabezado = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private bool _isBusy;

    /// <summary>
    /// Filas leídas hasta el momento. Con lectura streaming no se conoce el total por adelantado,
    /// por eso la barra es indeterminada y el avance se informa como cantidad de filas (H-04).
    /// </summary>
    [ObservableProperty]
    private int _filasLeidas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstado))]
    private string? _mensajeEstado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneResultado))]
    [NotifyPropertyChangedFor(nameof(TieneErroresDetalle))]
    private ResultadoImportacionDto? _ultimoResultado;

    public bool HayArchivoSeleccionado => !string.IsNullOrWhiteSpace(RutaArchivo);
    public bool TieneMensajeEstado => !string.IsNullOrWhiteSpace(MensajeEstado);
    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);
    public bool TieneResultado => UltimoResultado != null;

    /// <summary>Indica si hay filas con error para mostrar su detalle en el diálogo (H-01).</summary>
    public bool TieneErroresDetalle => UltimoResultado is { ErroresDetalle.Count: > 0 };

    public bool PuedeImportar =>
        !IsBusy &&
        Proveedor != null &&
        HayArchivoSeleccionado &&
        FilaEncabezado >= 1 &&
        !string.IsNullOrWhiteSpace(ColumnaCodigo) &&
        !string.IsNullOrWhiteSpace(ColumnaDescripcion) &&
        !string.IsNullOrWhiteSpace(ColumnaPrecio);

    public bool DialogResult { get; private set; }

    public ImportarPlanillaViewModel(
        IProveedorService proveedorService,
        ILogger<ImportarPlanillaViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Inicializar(ProveedorDto proveedor)
    {
        Proveedor = proveedor ?? throw new ArgumentNullException(nameof(proveedor));
        RutaArchivo = null;
        ColumnaCodigo = "CODIGO";
        ColumnaCodigoBarras = "EAN";
        ColumnaDescripcion = "DESCRIPCION";
        ColumnaPrecio = "PRECIO";
        FilaEncabezado = 1;
        IsBusy = false;
        FilasLeidas = 0;
        MensajeEstado = null;
        MensajeError = null;
        UltimoResultado = null;
        DialogResult = false;
    }

    [RelayCommand]
    private void ExaminarArchivo()
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Seleccionar planilla para {Proveedor?.RazonSocial ?? "proveedor"}",
            Filter = "Planillas Excel y CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            RutaArchivo = dialog.FileName;
            MensajeError = null;
            MensajeEstado = $"Archivo seleccionado: {Path.GetFileName(dialog.FileName)}";
        }
    }

    [RelayCommand]
    private async Task ImportarPlanillaAsync(CancellationToken cancellationToken)
    {
        if (!PuedeImportar || Proveedor is null || string.IsNullOrWhiteSpace(RutaArchivo))
        {
            return;
        }

        IsBusy = true;
        FilasLeidas = 0;
        MensajeError = null;
        MensajeEstado = "Iniciando lectura streaming de planilla con MiniExcel...";

        try
        {
            using var fileStream = File.OpenRead(RutaArchivo);

            var mapeo = new MapeoColumnasDto
            {
                IdProveedor = Proveedor.IdProveedor,
                ColumnaCodigo = ColumnaCodigo.Trim(),
                ColumnaCodigoBarras = string.IsNullOrWhiteSpace(ColumnaCodigoBarras) ? null : ColumnaCodigoBarras.Trim(),
                ColumnaDescripcion = ColumnaDescripcion.Trim(),
                ColumnaPrecioCosto = ColumnaPrecio.Trim(),
                FilaEncabezado = FilaEncabezado,
                ExtensionArchivo = Path.GetExtension(RutaArchivo)
            };

            var progreso = new Progress<int>(filas =>
            {
                FilasLeidas = filas;
                MensajeEstado = $"Leyendo planilla... ({filas:N0} filas leídas)";
            });

            UltimoResultado = await Task.Run(
                () => _proveedorService.ImportarPlanillaProveedorAsync(fileStream, mapeo, progreso, cancellationToken),
                cancellationToken);

            MensajeEstado = $"Importación finalizada con éxito en {UltimoResultado.TiempoTranscurrido.TotalSeconds:N1}s.";
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            MensajeEstado = "Importación cancelada.";
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested ||
                                   ex.InnerException is OperationCanceledException ||
                                   ex.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
        {
            MensajeEstado = "Importación cancelada.";
        }
        catch (InvalidDataException ex)
        {
            // Planilla que no coincide con el mapeo (columna o fila de encabezado inexistente):
            // es un error de configuración del usuario, no una falla del sistema.
            _logger.LogWarning(ex, "Planilla incompatible con el mapeo para el proveedor {IdProveedor}", Proveedor?.IdProveedor);
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la importación de planilla para el proveedor {IdProveedor}", Proveedor?.IdProveedor);
            MensajeError = $"Error al importar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cerrar(Window window)
    {
        window.DialogResult = DialogResult;
        window.Close();
    }
}
