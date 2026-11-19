using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Diálogo "Guardar como presupuesto" del Punto de venta (RF-11). Tiene dos estados: primero pide el título interno
/// opcional y la vigencia y emite el presupuesto; después ofrece imprimirlo o exportarlo a PDF. No exige turno de caja
/// abierto (PS-17). Los errores de dominio (por ejemplo, "el precio cambió") se muestran dentro del diálogo sin cerrarlo.
/// </summary>
public partial class GuardarPresupuestoViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    private readonly int _idUsuario;
    private readonly int? _idCliente;
    private readonly decimal _descuento;
    private readonly IReadOnlyList<DetallePresupuestoDto> _items;
    private readonly IPresupuestoService _presupuestoService;
    private readonly IPresupuestoDialogService _dialogService;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;
    private readonly FlujoExportarPdfPresupuesto _flujoPdf;

    /// <param name="idUsuario">Usuario de la sesión, que figura como quien emite el presupuesto.</param>
    /// <param name="idCliente">Cliente del ticket; <c>null</c> es Consumidor Final.</param>
    /// <param name="clienteNombre">Nombre que se muestra del cliente del ticket.</param>
    /// <param name="descuento">Descuento del ticket, que viaja al presupuesto (PS-15).</param>
    /// <param name="total">Total del ticket, solo para mostrarlo.</param>
    /// <param name="items">Líneas del ticket con el precio que muestra el POS como precio pactado.</param>
    /// <param name="acceso">Fila de accesos a datos del POS, para no usar su DbContext en paralelo (H-19).</param>
    /// <param name="numeroBase">Número del presupuesto cargado en el ticket, si lo hay (PE-15): el nuevo es independiente y ese no cambia.</param>
    /// <param name="tituloBase">Título del presupuesto base, que se precarga y puede editarse (PE-17).</param>
    /// <param name="preciosActualizados">Cantidad de precios que pasaron al catálogo de hoy respecto del base (PE-16).</param>
    public GuardarPresupuestoViewModel(
        int idUsuario,
        int? idCliente,
        string? clienteNombre,
        decimal descuento,
        decimal total,
        IReadOnlyList<DetallePresupuestoDto> items,
        IPresupuestoService presupuestoService,
        IPresupuestoDialogService dialogService,
        CargaSerializada acceso,
        ILogger logger,
        int? numeroBase = null,
        string? tituloBase = null,
        int preciosActualizados = 0)
    {
        _idUsuario = idUsuario;
        _idCliente = idCliente;
        _descuento = descuento;
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _flujoPdf = new FlujoExportarPdfPresupuesto(_dialogService, _logger);

        ClienteNombre = string.IsNullOrWhiteSpace(clienteNombre) ? "Consumidor Final" : clienteNombre;
        // C0: los importes de la app van sin centavos, como en el resto de los diálogos de presupuesto
        TotalFormateado = total.ToString("C0", CulturaArgentina);

        if (numeroBase is int numero)
        {
            AvisoBase = $"Se emite un presupuesto nuevo; el N.º {PresentacionPresupuesto.FormatearNumero(numero)} no cambia.";
            AvisoPrecios = preciosActualizados switch
            {
                <= 0 => null,
                1 => "1 precio pasa al catálogo de hoy.",
                _ => $"{preciosActualizados} precios pasan al catálogo de hoy."
            };
            Titulo = tituloBase ?? string.Empty;
        }
    }

    // ------------------------------------------------------------------ //
    // Estado 1: datos                                                     //
    // ------------------------------------------------------------------ //

    public string ClienteNombre { get; }

    public string TotalFormateado { get; }

    /// <summary>Aviso de "basado en" (PE-15); <c>null</c> si el ticket no viene de un presupuesto.</summary>
    public string? AvisoBase { get; }

    /// <summary>Cuántos precios pasan al catálogo de hoy (PE-16); <c>null</c> si ninguno cambia o no hay base.</summary>
    public string? AvisoPrecios { get; }

    public bool TieneAvisoBase => AvisoBase is not null;

    public bool TieneAvisoPrecios => AvisoPrecios is not null;

    // Límites del dominio, publicados para que la vista los tome con x:Static: una sola fuente de verdad (Presupuesto).
    public static int LongitudMaximaTitulo => Presupuesto.LongitudMaximaTitulo;

    public static double VigenciaMinimaDias => Presupuesto.VigenciaMinimaDias;

    public static double VigenciaMaximaDias => Presupuesto.VigenciaMaximaDias;

    /// <summary>Nota interna opcional (PS-20): solo se ve en el sistema, no se imprime ni se entrega al cliente.</summary>
    [ObservableProperty]
    private string _titulo = string.Empty;

    /// <summary>Días de validez. Es <c>double?</c> porque es el tipo del valor de <c>NumberBox</c> (<c>null</c> si lo borran).</summary>
    [ObservableProperty]
    private double? _vigenciaDias = Presupuesto.VigenciaPorDefectoDias;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    [NotifyPropertyChangedFor(nameof(NoEstaOcupado))]
    private bool _isBusy;

    public bool NoEstaOcupado => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    // ------------------------------------------------------------------ //
    // Estado 2: guardado                                                  //
    // ------------------------------------------------------------------ //

    /// <summary>El presupuesto ya se emitió: el diálogo pasa al estado 2 y el POS limpia el ticket al cerrarlo.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    [NotifyPropertyChangedFor(nameof(EstaEditando))]
    [NotifyPropertyChangedFor(nameof(TextoGuardado))]
    private bool _guardado;

    public bool EstaEditando => !Guardado;

    /// <summary>Presupuesto emitido; <c>null</c> mientras no se guardó.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoGuardado))]
    private PresupuestoDto? _presupuestoGuardado;

    /// <summary>"Presupuesto N.º 00000123 guardado · válido hasta 02/12/2026".</summary>
    public string TextoGuardado => PresupuestoGuardado is null
        ? string.Empty
        : $"Presupuesto N.º {PresentacionPresupuesto.FormatearNumero(PresupuestoGuardado.IdPresupuesto)} guardado · válido hasta " +
          PresupuestoGuardado.FechaVencimiento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ //
    // Acciones                                                            //
    // ------------------------------------------------------------------ //

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (VigenciaDias is not { } dias)
        {
            MensajeError = "Ingrese los días de vigencia.";
            return;
        }

        var dto = new CrearPresupuestoDto
        {
            IdUsuario = _idUsuario,
            IdCliente = _idCliente,
            Titulo = string.IsNullOrWhiteSpace(Titulo) ? null : Titulo.Trim(),
            Descuento = _descuento,
            VigenciaDias = (int)Math.Round(dias, MidpointRounding.AwayFromZero),
            Items = _items
        };

        IsBusy = true;
        MensajeError = null;

        try
        {
            var (completado, emitido) = await _acceso.EjecutarEnFilaAsync(token => _presupuestoService.EmitirAsync(dto, token));
            if (completado)
            {
                PresupuestoGuardado = emitido;
                Guardado = true;
            }
        }
        catch (ValidationException ex)
        {
            MensajeError = string.Join("\n", ex.Errors.Select(e => e.ErrorMessage).Distinct());
        }
        catch (DomainException ex)
        {
            // Regla de negocio (el precio cambió, un artículo se dio de baja): información para el cajero, no una falla.
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el presupuesto desde el Punto de venta");
            MensajeError = "No se pudo guardar el presupuesto. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeGuardar()
    {
        return !Guardado && !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreGuardado))]
    private async Task ImprimirAsync()
    {
        var idPresupuesto = PresupuestoGuardado!.IdPresupuesto;

        IsBusy = true;
        try
        {
            if (await _acceso.EjecutarOperacionEnFilaAsync(token => _presupuestoService.ImprimirAsync(idPresupuesto, token)))
            {
                _dialogService.MostrarInformacion(
                    "Presupuesto impreso",
                    $"Se envió el presupuesto N.º {PresentacionPresupuesto.FormatearNumero(idPresupuesto)} a la impresora de tickets.");
            }
        }
        catch (Exception ex)
        {
            InformarError(ex, idPresupuesto);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperarSobreGuardado))]
    private async Task ExportarPdfAsync()
    {
        var idPresupuesto = PresupuestoGuardado!.IdPresupuesto;

        IsBusy = true;
        try
        {
            await _flujoPdf.EjecutarAsync(
                idPresupuesto,
                destino => _acceso.EjecutarOperacionEnFilaAsync(token => _presupuestoService.ExportarPdfAsync(idPresupuesto, destino, token)));
        }
        catch (Exception ex)
        {
            InformarError(ex, idPresupuesto);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeOperarSobreGuardado()
    {
        return Guardado && !IsBusy;
    }

    private void InformarError(Exception ex, int idPresupuesto)
    {
        if (ex is DomainException dominio)
        {
            _dialogService.MostrarAlerta("No se pudo completar la operación", dominio.Message);
            return;
        }

        _logger.LogError(ex, "Error inesperado al operar sobre el presupuesto {IdPresupuesto}", idPresupuesto);
        _dialogService.MostrarError(
            "Error inesperado",
            "No se pudo completar la operación. El detalle quedó registrado en el log.");
    }
}
