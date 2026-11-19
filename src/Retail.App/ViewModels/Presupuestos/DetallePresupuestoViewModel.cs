using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Detalle de solo lectura de un presupuesto: cabecera, renglones y totales, con las acciones de imprimir el ticket y
/// exportar a PDF. Carga el presupuesto por su número al abrirse el diálogo.
/// </summary>
public partial class DetallePresupuestoViewModel : ObservableObject
{
    private readonly int _idPresupuesto;
    private readonly IPresupuestoService _presupuestoService;
    private readonly IPresupuestoDialogService _dialogService;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DetallePresupuestoViewModel> _logger;
    private readonly FlujoExportarPdfPresupuesto _flujoPdf;

    public DetallePresupuestoViewModel(
        int idPresupuesto,
        IPresupuestoService presupuestoService,
        IPresupuestoDialogService dialogService,
        TimeProvider reloj,
        ILogger<DetallePresupuestoViewModel> logger)
    {
        _idPresupuesto = idPresupuesto;
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _flujoPdf = new FlujoExportarPdfPresupuesto(_dialogService, _logger);
    }

    /// <summary>Número como se imprime (00000123).</summary>
    public string Numero => PresentacionPresupuesto.FormatearNumero(_idPresupuesto);

    public string TituloVentana => $"Presupuesto N.º {Numero}";

    // ------------------------------------------------------------------ //
    // Cabecera                                                            //
    // ------------------------------------------------------------------ //

    /// <summary>Título interno editable (PE-08): es lo único que se puede cambiar de un presupuesto ya emitido.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneTitulo))]
    [NotifyCanExecuteChangedFor(nameof(GuardarTituloCommand))]
    private string _titulo = string.Empty;

    /// <summary>Título tal como está guardado, para saber si el usuario lo modificó.</summary>
    private string _tituloGuardado = string.Empty;

    /// <summary>Límite del dominio, publicado para que la vista lo tome con <c>x:Static</c>.</summary>
    public static int LongitudMaximaTitulo => Presupuesto.LongitudMaximaTitulo;

    /// <summary>Se guardó un título nuevo: al cerrar el detalle, la pestaña recarga la grilla.</summary>
    public bool TituloCambiado { get; private set; }

    /// <summary>"Reemplaza al N.º 00000041" y/o "Reemplazado por N.º 00000045"; vacío si el presupuesto no está en una corrección.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneReemplazo))]
    private string _textoReemplazo = string.Empty;

    public bool TieneReemplazo => !string.IsNullOrWhiteSpace(TextoReemplazo);

    [ObservableProperty]
    private string _clienteTexto = "Consumidor Final";

    [ObservableProperty]
    private string _usuarioNombre = string.Empty;

    [ObservableProperty]
    private DateTime _emisionLocal;

    /// <summary>Último día de validez (inclusive) a medianoche, listo para formatear como fecha.</summary>
    [ObservableProperty]
    private DateTime _vencimiento;

    [ObservableProperty]
    private EstadoVisualPresupuesto _estadoVisual;

    public string EstadoTexto => PresentacionPresupuesto.TextoDe(EstadoVisual);

    /// <summary>Nota interna (PS-20): solo se muestra si el presupuesto tiene una.</summary>
    public bool TieneTitulo => !string.IsNullOrWhiteSpace(Titulo);

    // ------------------------------------------------------------------ //
    // Renglones y totales                                                 //
    // ------------------------------------------------------------------ //

    public ObservableCollection<DetallePresupuestoDto> Renglones { get; } = new();

    [ObservableProperty]
    private decimal _subtotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDescuento))]
    private decimal _descuento;

    [ObservableProperty]
    private decimal _total;

    /// <summary>El descuento solo se muestra si es mayor a cero.</summary>
    public bool TieneDescuento => Descuento > 0m;

    // ------------------------------------------------------------------ //
    // Estado de la pantalla                                               //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(GuardarTituloCommand))]
    private bool _cargado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarTituloCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>Lee el presupuesto y lo vuelca en pantalla. La invoca el diálogo al mostrarse.</summary>
    public async Task InicializarAsync()
    {
        IsBusy = true;
        MensajeError = null;

        try
        {
            var presupuesto = await Task.Run(() => _presupuestoService.ObtenerAsync(_idPresupuesto));
            if (presupuesto is null)
            {
                MensajeError = $"No se encontró el presupuesto N.º {Numero}.";
                return;
            }

            Mostrar(presupuesto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar el detalle del presupuesto {IdPresupuesto}", _idPresupuesto);
            MensajeError = "No se pudo cargar el presupuesto.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Mostrar(PresupuestoDto presupuesto)
    {
        _tituloGuardado = presupuesto.Titulo ?? string.Empty;
        Titulo = _tituloGuardado;
        TextoReemplazo = ComponerTextoReemplazo(presupuesto);
        ClienteTexto = string.IsNullOrWhiteSpace(presupuesto.ClienteNombre) ? "Consumidor Final" : presupuesto.ClienteNombre;
        UsuarioNombre = presupuesto.UsuarioNombre ?? string.Empty;
        EmisionLocal = PresentacionPresupuesto.ALocal(presupuesto.FechaEmision, _reloj);
        Vencimiento = PresentacionPresupuesto.ADia(presupuesto.FechaVencimiento);
        EstadoVisual = PresentacionPresupuesto.ResolverEstado(presupuesto.Estado, presupuesto.EstaVigente);
        OnPropertyChanged(nameof(EstadoTexto));

        Renglones.Clear();
        foreach (var renglon in presupuesto.Items)
        {
            Renglones.Add(renglon);
        }

        Subtotal = presupuesto.Subtotal;
        Descuento = presupuesto.Descuento;
        Total = presupuesto.Total;
        Cargado = true;
    }

    private static string ComponerTextoReemplazo(PresupuestoDto presupuesto)
    {
        var partes = new List<string>();

        if (presupuesto.IdPresupuestoReemplazado is int reemplazado)
        {
            partes.Add($"Reemplaza al N.º {PresentacionPresupuesto.FormatearNumero(reemplazado)}");
        }

        if (presupuesto.IdPresupuestoReemplazante is int reemplazante)
        {
            partes.Add($"Reemplazado por N.º {PresentacionPresupuesto.FormatearNumero(reemplazante)}");
        }

        return string.Join(" · ", partes);
    }

    // ------------------------------------------------------------------ //
    // Acciones                                                            //
    // ------------------------------------------------------------------ //

    private bool PuedeGuardarTitulo()
    {
        return Cargado && !IsBusy && Titulo.Trim() != _tituloGuardado;
    }

    /// <summary>
    /// Guarda el título interno (PE-08). Vale en cualquier estado porque el título nunca se imprime ni se exporta (PS-20):
    /// cambiarlo no altera ningún documento entregado. Un error del dominio se muestra en el diálogo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeGuardarTitulo))]
    private async Task GuardarTituloAsync()
    {
        var nuevo = Titulo.Trim();

        IsBusy = true;
        MensajeError = null;

        try
        {
            await Task.Run(() => _presupuestoService.CambiarTituloAsync(_idPresupuesto, nuevo.Length == 0 ? null : nuevo));

            _tituloGuardado = nuevo;
            Titulo = nuevo;
            TituloCambiado = true;
        }
        catch (DomainException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar el título del presupuesto {IdPresupuesto}", _idPresupuesto);
            MensajeError = "No se pudo guardar el título. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(Cargado))]
    private async Task ImprimirAsync()
    {
        IsBusy = true;
        try
        {
            await Task.Run(() => _presupuestoService.ImprimirAsync(_idPresupuesto));
            _dialogService.MostrarInformacion("Presupuesto impreso", $"Se envió el presupuesto N.º {Numero} a la impresora de tickets.");
        }
        catch (Exception ex)
        {
            InformarError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(Cargado))]
    private async Task ExportarPdfAsync()
    {
        IsBusy = true;
        try
        {
            await _flujoPdf.EjecutarAsync(
                _idPresupuesto,
                destino => Task.Run(() => _presupuestoService.ExportarPdfAsync(_idPresupuesto, destino)));
        }
        catch (Exception ex)
        {
            InformarError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InformarError(Exception ex)
    {
        if (ex is DomainException dominio)
        {
            _dialogService.MostrarAlerta("No se pudo completar la operación", dominio.Message);
            return;
        }

        _logger.LogError(ex, "Error inesperado al operar sobre el presupuesto {IdPresupuesto}", _idPresupuesto);
        _dialogService.MostrarError(
            "Error inesperado",
            "No se pudo completar la operación. El detalle quedó registrado en el log.");
    }
}
