using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Comun;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Editor de un presupuesto (módulo 5.4, IU-13): sirve para "Nuevo presupuesto" (PE-01) y para "Corregir" (PE-02). Compone el
/// <see cref="EditorRenglonesViewModel"/> y suma título interno, cliente, vigencia y descuento. Tiene dos estados: primero
/// arma el documento y lo emite (o lo reemplaza, al corregir); después ofrece imprimirlo o exportarlo a PDF. Los errores de
/// dominio se muestran dentro del diálogo sin cerrarlo; si un precio cambió mientras estaba abierto (PE-10) ofrece
/// <b>Actualizar precios</b> en lugar de obligar a recargar todo.
/// </summary>
public partial class PresupuestoFormViewModel : ObservableObject
{
    private const string ConsumidorFinal = "Consumidor Final";

    private readonly PresupuestoParaVentaDto? _aCorregir;
    private readonly IPresupuestoService _presupuestoService;
    private readonly IVentaService _ventaService;
    private readonly IClienteService _clienteService;
    private readonly IVentaDialogService _ventaDialogService;
    private readonly IPresupuestoDialogService _dialogService;
    private readonly ICurrentUserSession _sesion;
    private readonly CargaSerializada _acceso;
    private readonly TimeProvider _reloj;
    private readonly ILogger _logger;
    private readonly FlujoExportarPdfPresupuesto _flujoPdf;

    // Avisos fijos de la corrección (artículos omitidos y precios que cambiaron), calculados al abrir el editor.
    private readonly List<string> _avisos = new();

    /// <param name="aCorregir">Presupuesto pendiente a corregir, leído al abrir el editor, o <c>null</c> para crear uno nuevo.</param>
    /// <param name="acceso">Fila de accesos a datos de la pantalla que abre el editor, para no usar su DbContext en paralelo (H-19).</param>
    public PresupuestoFormViewModel(
        PresupuestoParaVentaDto? aCorregir,
        IPresupuestoService presupuestoService,
        IVentaService ventaService,
        IClienteService clienteService,
        IVentaDialogService ventaDialogService,
        IPresupuestoDialogService dialogService,
        ICurrentUserSession sesion,
        CargaSerializada acceso,
        TimeProvider reloj,
        ILogger logger)
    {
        _aCorregir = aCorregir;
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _ventaService = ventaService ?? throw new ArgumentNullException(nameof(ventaService));
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
        _ventaDialogService = ventaDialogService ?? throw new ArgumentNullException(nameof(ventaDialogService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _flujoPdf = new FlujoExportarPdfPresupuesto(_dialogService, _logger);

        Editor = new EditorRenglonesViewModel(
            ventaService,
            acceso,
            logger,
            exigirAlMenosUnRenglon: false,
            textoQuitar: "Quitar el artículo del presupuesto");
        Editor.ContenidoCambiado += (_, _) => ActualizarTotales();
        Editor.MensajeEmitido += (_, mensaje) => MensajeError = mensaje;

        if (aCorregir is not null)
        {
            PrecargarCorreccion(aCorregir);
        }

        ActualizarTotales();
    }

    // ------------------------------------------------------------------ //
    // Modo                                                                //
    // ------------------------------------------------------------------ //

    public bool EsCorreccion => _aCorregir is not null;

    public string TituloVentana => _aCorregir is null
        ? "Nuevo presupuesto"
        : $"Corregir presupuesto N.º {PresentacionPresupuesto.FormatearNumero(_aCorregir.IdPresupuesto)}";

    /// <summary>Buscador, renglones y aviso de artículos dados de baja (componente compartido con el editor de listas).</summary>
    public EditorRenglonesViewModel Editor { get; }

    // Límites del dominio, publicados para que la vista los tome con x:Static: una sola fuente de verdad (Presupuesto).
    public static int LongitudMaximaTitulo => Presupuesto.LongitudMaximaTitulo;

    public static double VigenciaMinimaDias => Presupuesto.VigenciaMinimaDias;

    public static double VigenciaMaximaDias => Presupuesto.VigenciaMaximaDias;

    private void PrecargarCorreccion(PresupuestoParaVentaDto original)
    {
        // PE-06: el corregido es una cotización nueva, con los precios de catálogo de hoy; PE-11: los dados de baja no se cargan.
        Titulo = original.Titulo ?? string.Empty;
        Descuento = (double)original.Descuento;

        Editor.CargarRenglones(original.Renglones.Select(r => new RenglonEditableFila(
            r.IdArticulo,
            r.CodigoBarras,
            r.Descripcion,
            r.PrecioActual,
            r.Cantidad,
            estaDisponible: true)));

        if (original.ArticulosNoDisponibles.Count > 0)
        {
            _avisos.Add(original.ArticulosNoDisponibles.Count == 1
                ? $"Se omitió un artículo dado de baja: {original.ArticulosNoDisponibles[0]}."
                : $"Se omitieron {original.ArticulosNoDisponibles.Count} artículos dados de baja: {string.Join(", ", original.ArticulosNoDisponibles)}.");
        }

        int preciosCambiados = original.Renglones.Count(r => r.PrecioActual != r.PrecioPactado);
        if (preciosCambiados > 0)
        {
            _avisos.Add(preciosCambiados == 1
                ? "1 precio cambió respecto del presupuesto original."
                : $"{preciosCambiados} precios cambiaron respecto del presupuesto original.");
        }

        MensajeAviso = ComponerAviso();
    }

    private string? ComponerAviso()
    {
        return _avisos.Count == 0 ? null : string.Join("\n", _avisos);
    }

    /// <summary>
    /// Resuelve el cliente del presupuesto que se corrige (R-2): si fue dado de baja, queda Consumidor Final y se avisa.
    /// La invoca el diálogo al mostrarse.
    /// </summary>
    public async Task InicializarAsync()
    {
        if (_aCorregir?.IdCliente is not int idCliente)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (completado, cliente) = await _acceso.EjecutarEnFilaAsync(token => _clienteService.ObtenerClientePorIdAsync(idCliente, token));
            if (!completado)
            {
                return;
            }

            if (cliente is null)
            {
                _avisos.Add("El cliente del presupuesto original ya no existe: quedó como Consumidor Final.");
                MensajeAviso = ComponerAviso();
            }
            else
            {
                Cliente = cliente;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo buscar el cliente {IdCliente} del presupuesto {IdPresupuesto}", idCliente, _aCorregir.IdPresupuesto);
            _avisos.Add("No se pudo cargar el cliente del presupuesto original: quedó como Consumidor Final.");
            MensajeAviso = ComponerAviso();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------ //
    // Datos del presupuesto                                               //
    // ------------------------------------------------------------------ //

    /// <summary>Nota interna opcional (PS-20): solo se ve en el sistema, no se imprime ni se entrega al cliente.</summary>
    [ObservableProperty]
    private string _titulo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClienteNombre))]
    [NotifyPropertyChangedFor(nameof(TieneCliente))]
    private ClienteDto? _cliente;

    public string ClienteNombre => Cliente?.RazonSocialONombre ?? ConsumidorFinal;

    public bool TieneCliente => Cliente is not null;

    /// <summary>Días de validez. Es <c>double?</c> porque es el tipo del valor de <c>NumberBox</c> (<c>null</c> si lo borran).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VigenciaEsValida))]
    [NotifyPropertyChangedFor(nameof(TieneErrorVigencia))]
    [NotifyPropertyChangedFor(nameof(TextoErrorVigencia))]
    [NotifyPropertyChangedFor(nameof(TextoValidoHasta))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private double? _vigenciaDias = Presupuesto.VigenciaPorDefectoDias;

    public bool VigenciaEsValida => VigenciaDias is { } dias && dias >= VigenciaMinimaDias && dias <= VigenciaMaximaDias;

    public bool TieneErrorVigencia => !VigenciaEsValida;

    public string? TextoErrorVigencia => VigenciaEsValida
        ? null
        : $"Ingrese entre {Presupuesto.VigenciaMinimaDias} y {Presupuesto.VigenciaMaximaDias} días.";

    /// <summary>"Válido hasta 04/12/2026": el último día de validez, contado desde hoy (PS-09, PE-05).</summary>
    public string TextoValidoHasta => VigenciaEsValida
        ? $"Válido hasta {FechaVencimiento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
        : string.Empty;

    private DateTime FechaVencimiento => _reloj.GetLocalNow().Date.AddDays((int)Math.Round(VigenciaDias ?? 0, MidpointRounding.AwayFromZero));

    /// <summary>Descuento en pesos. Es <c>double?</c> porque es el tipo del valor de <c>NumberBox</c> (<c>null</c> si lo borran).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescuentoEsValido))]
    [NotifyPropertyChangedFor(nameof(TieneErrorDescuento))]
    [NotifyPropertyChangedFor(nameof(TextoErrorDescuento))]
    [NotifyPropertyChangedFor(nameof(DescuentoAplicado))]
    [NotifyPropertyChangedFor(nameof(Total))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private double? _descuento = 0d;

    private decimal DescuentoIngresado => (decimal)(Descuento ?? 0d);

    public bool DescuentoEsValido => DescuentoIngresado >= 0m && DescuentoIngresado <= Subtotal;

    public bool TieneErrorDescuento => !DescuentoEsValido;

    public string? TextoErrorDescuento => DescuentoEsValido ? null : "No puede ser negativo ni mayor al subtotal.";

    /// <summary>Descuento que se resta en los totales: cero si lo ingresado no es válido.</summary>
    public decimal DescuentoAplicado => DescuentoEsValido ? DescuentoIngresado : 0m;

    public decimal Subtotal => Editor.Subtotal;

    public decimal Total => Math.Max(0m, Subtotal - DescuentoAplicado);

    private void ActualizarTotales()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DescuentoEsValido));
        OnPropertyChanged(nameof(TieneErrorDescuento));
        OnPropertyChanged(nameof(TextoErrorDescuento));
        OnPropertyChanged(nameof(DescuentoAplicado));
        OnPropertyChanged(nameof(Total));
        GuardarCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SeleccionarClienteAsync()
    {
        // El modal es el mismo del POS (F4): devuelve el cliente elegido o, si se cancela, el actual.
        Cliente = await _ventaDialogService.MostrarSeleccionarClienteModalAsync(Cliente);
    }

    [RelayCommand]
    private void QuitarCliente()
    {
        Cliente = null;
    }

    // ------------------------------------------------------------------ //
    // Estado                                                              //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActualizarPreciosCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>Aviso informativo (artículos omitidos, precios que cambiaron, cliente que ya no existe); no impide guardar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAviso))]
    private string? _mensajeAviso;

    public bool TieneAviso => !string.IsNullOrWhiteSpace(MensajeAviso);

    /// <summary>Hay precios del catálogo distintos de los que se ven: se ofrece <b>Actualizar precios</b> (PE-10).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActualizarPreciosCommand))]
    private bool _hayPreciosDesactualizados;

    /// <summary>El presupuesto ya se emitió: el diálogo pasa al estado 2 y la pestaña recarga al cerrarlo.</summary>
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

    /// <summary>
    /// "Presupuesto N.º 00000123 guardado · válido hasta 02/12/2026" o, al corregir, "Se emitió el presupuesto N.º 00000045,
    /// que reemplaza al N.º 00000041."
    /// </summary>
    public string TextoGuardado
    {
        get
        {
            if (PresupuestoGuardado is null)
            {
                return string.Empty;
            }

            var numero = PresentacionPresupuesto.FormatearNumero(PresupuestoGuardado.IdPresupuesto);

            return _aCorregir is null
                ? $"Presupuesto N.º {numero} guardado · válido hasta " +
                  PresupuestoGuardado.FechaVencimiento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : $"Se emitió el presupuesto N.º {numero}, que reemplaza al N.º {PresentacionPresupuesto.FormatearNumero(_aCorregir.IdPresupuesto)}.";
        }
    }

    // ------------------------------------------------------------------ //
    // Guardar                                                             //
    // ------------------------------------------------------------------ //

    private bool PuedeGuardar()
    {
        return !Guardado
            && !IsBusy
            && VigenciaEsValida
            && DescuentoEsValido
            && Editor.Renglones.Any(r => r.EstaDisponible);
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (_sesion.IdUsuario is not int idUsuario)
        {
            MensajeError = "No hay una sesión activa. Inicie sesión nuevamente.";
            return;
        }

        var dto = new CrearPresupuestoDto
        {
            IdUsuario = idUsuario,
            IdCliente = Cliente?.IdCliente,
            Titulo = string.IsNullOrWhiteSpace(Titulo) ? null : Titulo.Trim(),
            Descuento = DescuentoIngresado,
            VigenciaDias = (int)Math.Round(VigenciaDias!.Value, MidpointRounding.AwayFromZero),
            Items = ArmarItems()
        };

        IsBusy = true;
        MensajeError = null;
        HayPreciosDesactualizados = false;

        try
        {
            var (completado, emitido) = _aCorregir is null
                ? await _acceso.EjecutarEnFilaAsync(token => _presupuestoService.EmitirAsync(dto, token))
                : await _acceso.EjecutarEnFilaAsync(token => _presupuestoService.ReemplazarAsync(_aCorregir.IdPresupuesto, dto, token));

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
            // Regla de negocio (el precio cambió, un artículo se dio de baja, el original cambió de estado): información, no una falla.
            MensajeError = ex.Message;
            if (ex is not TransicionPresupuestoInvalidaException)
            {
                HayPreciosDesactualizados = await HayPreciosDistintosAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el presupuesto desde el editor");
            MensajeError = "No se pudo guardar el presupuesto. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Renglones disponibles con el precio que el usuario VE como precio pactado: el servidor lo compara con el catálogo (D-11).</summary>
    private List<DetallePresupuestoDto> ArmarItems()
    {
        return Editor.Renglones
            .Where(r => r.EstaDisponible)
            .Select(r => new DetallePresupuestoDto
            {
                IdArticulo = r.IdArticulo,
                CodigoBarras = r.CodigoBarras,
                Descripcion = r.Descripcion,
                Cantidad = r.Cantidad,
                PrecioUnitarioPactado = r.PrecioUnitario
            })
            .ToList();
    }

    // ------------------------------------------------------------------ //
    // Actualizar precios (PE-10)                                          //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Compara los renglones con el catálogo con el mismo servicio que el POS usa antes de cobrar
    /// (<see cref="IVentaService.VerificarTicketAsync"/>): informa los precios que cambiaron y los artículos dados de baja.
    /// </summary>
    private async Task<VerificacionTicketDto?> VerificarRenglonesAsync()
    {
        var items = Editor.Renglones
            .Where(r => r.EstaDisponible)
            .Select(r => new DetalleVentaDto
            {
                IdArticulo = r.IdArticulo,
                CodigoBarras = r.CodigoBarras,
                Descripcion = r.Descripcion,
                Cantidad = r.Cantidad,
                PrecioUnitario = r.PrecioUnitario
            })
            .ToList();

        var (completado, verificacion) = await _acceso.EjecutarEnFilaAsync(token => _ventaService.VerificarTicketAsync(items, null, token));

        return completado ? verificacion : null;
    }

    private async Task<bool> HayPreciosDistintosAsync()
    {
        try
        {
            var verificacion = await VerificarRenglonesAsync();
            return verificacion is { PreciosActualizados.Count: > 0 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo verificar los precios del presupuesto contra el catálogo");
            return false;
        }
    }

    private bool PuedeActualizarPrecios()
    {
        return HayPreciosDesactualizados && !Guardado && !IsBusy;
    }

    /// <summary>Vuelve a leer el precio de catálogo de todos los renglones y conserva todo lo demás cargado.</summary>
    [RelayCommand(CanExecute = nameof(PuedeActualizarPrecios))]
    private async Task ActualizarPreciosAsync()
    {
        IsBusy = true;

        try
        {
            var verificacion = await VerificarRenglonesAsync();
            if (verificacion is null)
            {
                return;
            }

            Editor.ActualizarPrecios(verificacion.PreciosActualizados.ToDictionary(p => p.IdArticulo, p => p.PrecioActual));

            HayPreciosDesactualizados = false;
            MensajeError = verificacion.ArticulosNoDisponibles.Count > 0
                ? $"Quite del presupuesto los artículos dados de baja: {string.Join(", ", verificacion.ArticulosNoDisponibles)}."
                : null;
            MensajeAviso = ComponerAvisoDePrecios(verificacion.PreciosActualizados.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar los precios del presupuesto");
            MensajeError = "No se pudieron actualizar los precios. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string ComponerAvisoDePrecios(int cantidad)
    {
        var avisoPrecios = cantidad switch
        {
            0 => "Los precios ya estaban al día.",
            1 => "Se actualizó 1 precio. Revise el total y guarde de nuevo.",
            _ => $"Se actualizaron {cantidad} precios. Revise el total y guarde de nuevo."
        };

        return string.Join("\n", _avisos.Append(avisoPrecios));
    }

    // ------------------------------------------------------------------ //
    // Estado 2: acciones sobre el documento                               //
    // ------------------------------------------------------------------ //

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
