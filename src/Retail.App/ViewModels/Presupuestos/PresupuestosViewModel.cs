using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Comun;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Consulta de presupuestos (IU-13, RF-11 y RF-12): grilla paginada en SQL con búsqueda y filtros de columna (estado,
/// cliente, emisión y vencimiento), y las
/// acciones de cada fila (ver detalle, imprimir, exportar a PDF, corregir y anular), más "Nuevo presupuesto" (módulo 5.4,
/// PE-01). Los presupuestos también se crean y se recuperan desde el Punto de venta (F3 y F7).
/// </summary>
public partial class PresupuestosViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    // Id local de la opción "Consumidor Final" del filtro de cliente: los ids reales empiezan en 1, así que 0 no choca.
    private const int IdConsumidorFinal = 0;

    private readonly IPresupuestoService _presupuestoService;
    private readonly IPresupuestoDialogService _dialogService;
    private readonly TimeProvider _reloj;
    private readonly ILogger<PresupuestosViewModel> _logger;
    private readonly FlujoExportarPdfPresupuesto _flujoPdf;

    // Una sola fila de accesos para el DbContext de la pantalla (H-19): la grilla usa la carga reemplazable (una búsqueda
    // nueva cancela la anterior) y las acciones de la fila, operaciones que ninguna carga nueva cancela.
    private readonly CargaSerializada _carga;

    // Evita que un cambio hecho por código (limpiar filtros) dispare su propia recarga.
    private bool _suspendiendoRecargas;

    // Hubo al menos una respuesta del servidor: antes de eso la grilla vacía no significa "no hay presupuestos".
    private bool _huboRespuesta;

    // La página pedida quedó fuera de rango (se anuló el último presupuesto de la última página): hay que retroceder.
    private bool _paginaFueraDeRango;

    public PresupuestosViewModel(
        IPresupuestoService presupuestoService,
        IPresupuestoDialogService dialogService,
        TimeProvider reloj,
        ILogger<PresupuestosViewModel> logger)
    {
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _carga = new CargaSerializada(_logger);
        _flujoPdf = new FlujoExportarPdfPresupuesto(_dialogService, _logger);

        // Los estados son fijos; los clientes llegan al inicializar la pantalla.
        FiltroEstado.CargarOpciones(
        [
            new OpcionFiltroColumna((int)FiltroEstadoPresupuestoEnum.Vigentes, "Vigentes"),
            new OpcionFiltroColumna((int)FiltroEstadoPresupuestoEnum.Vencidos, "Vencidos"),
            new OpcionFiltroColumna((int)FiltroEstadoPresupuestoEnum.Convertidos, "Convertidos"),
            new OpcionFiltroColumna((int)FiltroEstadoPresupuestoEnum.Anulados, "Anulados"),
            new OpcionFiltroColumna((int)FiltroEstadoPresupuestoEnum.Reemplazados, "Reemplazados")
        ]);

        FiltroEmision = new FiltroFechaColumnaViewModel("Emisión", _reloj);
        FiltroVencimiento = new FiltroFechaColumnaViewModel("Vence", _reloj);

        FiltroEstado.Aplicado += AlAplicarFiltroDeColumna;
        FiltroCliente.Aplicado += AlAplicarFiltroDeColumna;
        FiltroEmision.Aplicado += AlAplicarFiltroDeColumna;
        FiltroVencimiento.Aplicado += AlAplicarFiltroDeColumna;
    }

    // ------------------------------------------------------------------ //
    // Filtros y grilla                                                    //
    // ------------------------------------------------------------------ //

    public ObservableCollection<PresupuestoFila> Presupuestos { get; } = new();

    /// <summary>Búsqueda por número, título o cliente (se resuelve en SQL, con una espera para no consultar por cada tecla).</summary>
    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    /// <summary>Filtro de la columna Estado: vigentes, vencidos, convertidos, anulados y reemplazados (vacío = todos).</summary>
    public FiltroColumnaViewModel FiltroEstado { get; } = new("Estado");

    /// <summary>Filtro de la columna Cliente: Consumidor Final y los clientes con algún presupuesto; llegan al inicializar.</summary>
    public FiltroColumnaViewModel FiltroCliente { get; } = new("Cliente");

    /// <summary>Filtro de la columna Emisión: un rango de días locales; la consulta lo convierte a UTC.</summary>
    public FiltroFechaColumnaViewModel FiltroEmision { get; }

    /// <summary>Filtro de la columna Vence: un rango de fechas de calendario locales, ambos extremos incluidos.</summary>
    public FiltroFechaColumnaViewModel FiltroVencimiento { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private int _totalRegistros;

    [ObservableProperty]
    private int _tamanoPagina = 15;

    [ObservableProperty]
    private PresupuestoFila? _presupuestoSeleccionado;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = OpcionesPaginacion.Tamanos;

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public bool HayFiltrosActivos =>
        !string.IsNullOrWhiteSpace(TextoBusqueda) ||
        FiltroEstado.HayFiltroActivo ||
        FiltroCliente.HayFiltroActivo ||
        FiltroEmision.HayFiltroActivo ||
        FiltroVencimiento.HayFiltroActivo;

    // ------------------------------------------------------------------ //
    // Estado de la pantalla                                               //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>La consulta terminó sin errores y no devolvió filas: se muestra el estado vacío.</summary>
    public bool EstaVacio => _huboRespuesta && !IsBusy && !TieneMensajeError && Presupuestos.Count == 0;

    /// <summary>Texto del estado vacío: orienta al usuario si no hay presupuestos o dice que el filtro no encontró ninguno.</summary>
    public string TextoVacio => HayFiltrosActivos
        ? "Ningún presupuesto coincide con la búsqueda o los filtros."
        : "Todavía no hay presupuestos. Cree uno con «Nuevo presupuesto» o desde el Punto de venta.";

    // ------------------------------------------------------------------ //
    // Carga                                                               //
    // ------------------------------------------------------------------ //

    /// <summary>Carga los clientes del filtro y la primera página. La invoca la vista al mostrarse.</summary>
    public async Task InicializarAsync()
    {
        // Cargar las opciones no debe disparar una consulta propia: la primera página se consulta una sola vez, abajo.
        _suspendiendoRecargas = true;
        try
        {
            // Ley 4: la consulta corre fuera del hilo de UI y en la misma fila de accesos que la grilla.
            await _carga.EjecutarOperacionAsync(
                token => Task.Run(() => _presupuestoService.ListarClientesConPresupuestosAsync(token), token),
                MostrarClientes,
                ex =>
                {
                    _logger.LogError(ex, "Error al cargar los clientes del filtro de presupuestos");
                    MensajeError = "No se pudo cargar la lista de clientes.";
                });
        }
        finally
        {
            _suspendiendoRecargas = false;
        }

        PaginaActual = 1;
        await CargarPresupuestosAsync();
    }

    /// <summary>
    /// Consulta la página actual con los filtros vigentes. <paramref name="espera"/> es un debounce: si llega otra
    /// consulta antes de que venza, esta ni siquiera se ejecuta.
    /// </summary>
    public async Task CargarPresupuestosAsync(TimeSpan espera = default, CancellationToken cancellationToken = default)
    {
        var (emisionDesdeUtc, emisionHastaUtc) = FiltroEmision.RangoUtc();

        // Consumidor Final no es un cliente de la base: la opción usa un id local (IdConsumidorFinal) que acá se traduce
        // a la bandera de la consulta, y los demás ids son clientes reales.
        var idsCliente = FiltroCliente.IdsAplicados;

        var consulta = new ConsultaPresupuestosDto
        {
            TerminoBusqueda = TextoBusqueda,
            Estados = FiltroEstado.IdsAplicados.Select(id => (FiltroEstadoPresupuestoEnum)id).ToList(),
            IdsCliente = idsCliente.Where(id => id != IdConsumidorFinal).ToList(),
            IncluirConsumidorFinal = idsCliente.Contains(IdConsumidorFinal),
            EmisionDesdeUtc = emisionDesdeUtc,
            EmisionHastaUtc = emisionHastaUtc,
            VenceDesde = FiltroVencimiento.DesdeAplicado is { } venceDesde ? DateOnly.FromDateTime(venceDesde) : null,
            VenceHasta = FiltroVencimiento.HastaAplicado is { } venceHasta ? DateOnly.FromDateTime(venceHasta) : null,
            Pagina = PaginaActual,
            TamanoPagina = TamanoPagina
        };

        IsBusy = true;
        MensajeError = null;

        // Ley 4: la consulta corre fuera del hilo de UI y sobre la fila de accesos de la pantalla.
        await _carga.EjecutarAsync(
            token => Task.Run(() => _presupuestoService.ListarAsync(consulta, token), token),
            MostrarPresupuestos,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar los presupuestos");
                MensajeError = "No se pudieron cargar los presupuestos.";
            },
            espera,
            cancellationToken);

        IsBusy = _carga.EnCurso;

        if (_paginaFueraDeRango)
        {
            _paginaFueraDeRango = false;
            PaginaActual = TotalPaginas;
            await CargarPresupuestosAsync(cancellationToken: cancellationToken);
        }
    }

    [RelayCommand]
    private Task RecargarAsync()
    {
        return CargarPresupuestosAsync();
    }

    private void MostrarPresupuestos(PresupuestosPaginadosDto resultado)
    {
        Presupuestos.Clear();
        foreach (var resumen in resultado.Items)
        {
            Presupuestos.Add(new PresupuestoFila(resumen, _reloj));
        }

        TotalRegistros = resultado.TotalRegistros;
        TotalPaginas = Math.Max(1, resultado.TotalPaginas);
        _huboRespuesta = true;

        // Si la página pedida ya no existe (se anuló el último presupuesto de la última página), se retrocede
        _paginaFueraDeRango = resultado.Items.Count == 0 && PaginaActual > TotalPaginas;

        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(EstaVacio));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        ActualizarEstadoFiltros();
        RecargarDesdePrimeraPagina(EsperaBusqueda);
    }

    private void AlAplicarFiltroDeColumna(object? sender, EventArgs e)
    {
        ActualizarEstadoFiltros();
        RecargarDesdePrimeraPagina(TimeSpan.Zero);
    }

    private void MostrarClientes(IReadOnlyList<ClienteConPresupuestosDto> clientes)
    {
        // Consumidor Final va primero y siempre está: los presupuestos sin cliente existen aunque ningún cliente tenga uno.
        var opciones = new List<OpcionFiltroColumna> { new(IdConsumidorFinal, "Consumidor Final") };
        opciones.AddRange(clientes
            .OrderBy(c => c.Nombre)
            .Select(c => new OpcionFiltroColumna(c.IdCliente, c.Nombre)));

        FiltroCliente.CargarOpciones(opciones);
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        RecargarDesdePrimeraPagina(TimeSpan.Zero);
    }

    [RelayCommand(CanExecute = nameof(HayFiltrosActivos))]
    public void LimpiarFiltros()
    {
        // Se cambian sin disparar su propia recarga: la pantalla consulta una sola vez, al final.
        _suspendiendoRecargas = true;
        try
        {
            TextoBusqueda = string.Empty;
        }
        finally
        {
            _suspendiendoRecargas = false;
        }

        FiltroEstado.QuitarFiltro();
        FiltroCliente.QuitarFiltro();
        FiltroEmision.QuitarFiltro();
        FiltroVencimiento.QuitarFiltro();

        ActualizarEstadoFiltros();
        RecargarDesdePrimeraPagina(TimeSpan.Zero);
    }

    private void ActualizarEstadoFiltros()
    {
        OnPropertyChanged(nameof(HayFiltrosActivos));
        OnPropertyChanged(nameof(TextoVacio));
        LimpiarFiltrosCommand.NotifyCanExecuteChanged();
    }

    private void RecargarDesdePrimeraPagina(TimeSpan espera)
    {
        if (_suspendiendoRecargas)
        {
            return;
        }

        PaginaActual = 1;
        _ = CargarPresupuestosAsync(espera);
    }

    // ------------------------------------------------------------------ //
    // Paginación                                                          //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarPresupuestosAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarPresupuestosAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarPresupuestosAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarPresupuestosAsync();
        }
    }

    /// <summary>Va a una página puntual (los números de la barra de paginación); fuera de rango no hace nada.</summary>
    [RelayCommand]
    public async Task IrAPaginaAsync(int pagina)
    {
        if (pagina >= 1 && pagina <= TotalPaginas && pagina != PaginaActual)
        {
            PaginaActual = pagina;
            await CargarPresupuestosAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Acciones de la fila                                                 //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Abre el detalle. Lo disparan el botón de la fila, el doble clic y Enter. Si allí se cambió el título interno (PE-08),
    /// la grilla se recarga.
    /// </summary>
    [RelayCommand]
    private async Task VerDetalleAsync(PresupuestoFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        if (_dialogService.MostrarDetalle(fila.IdPresupuesto))
        {
            await CargarPresupuestosAsync();
        }
    }

    /// <summary>Abre el editor con un presupuesto vacío (PE-01) y recarga si se emitió.</summary>
    [RelayCommand]
    private async Task NuevoPresupuestoAsync()
    {
        if (_dialogService.AbrirEditor(null, _carga))
        {
            await CargarPresupuestosAsync();
        }
    }

    /// <summary>
    /// Corrige un presupuesto pendiente (PE-02): lo lee con el precio de hoy (PE-06) y abre el editor precargado. Recarga la
    /// grilla si se emitió el reemplazo, y también si la lectura falló porque otra terminal cambió el estado: la pantalla tiene
    /// que mostrar lo que hay en la base.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeCorregir))]
    private async Task CorregirAsync(PresupuestoFila? fila)
    {
        if (fila is null || !fila.PuedeCorregir)
        {
            return;
        }

        PresupuestoParaVentaDto? original = null;
        IsBusy = true;
        try
        {
            bool completado;
            (completado, original) = await _carga.EjecutarEnFilaAsync(token => _presupuestoService.RecuperarParaVentaAsync(fila.IdPresupuesto, token));
            if (!completado)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            ManejarError(ex);
            await CargarPresupuestosAsync();
            return;
        }
        finally
        {
            IsBusy = _carga.EnCurso;
        }

        if (original is not null && _dialogService.AbrirEditor(original, _carga))
        {
            await CargarPresupuestosAsync();
        }
    }

    private static bool PuedeCorregir(PresupuestoFila? fila)
    {
        return fila?.PuedeCorregir == true;
    }

    [RelayCommand]
    private async Task ImprimirAsync(PresupuestoFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await EjecutarEnAccesoAsync(token => _presupuestoService.ImprimirAsync(fila.IdPresupuesto, token));
            _dialogService.MostrarInformacion("Presupuesto impreso", $"Se envió el presupuesto N.º {fila.Numero} a la impresora de tickets.");
        }
        catch (Exception ex)
        {
            ManejarError(ex);
        }
        finally
        {
            IsBusy = _carga.EnCurso;
        }
    }

    [RelayCommand]
    private async Task ExportarPdfAsync(PresupuestoFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _flujoPdf.EjecutarAsync(
                fila.IdPresupuesto,
                destino => EjecutarEnAccesoAsync(token => _presupuestoService.ExportarPdfAsync(fila.IdPresupuesto, destino, token)));
        }
        catch (Exception ex)
        {
            ManejarError(ex);
        }
        finally
        {
            IsBusy = _carga.EnCurso;
        }
    }

    /// <summary>
    /// Anula un presupuesto pendiente tras pedir confirmación (sin motivo, PS-16) y recarga la página actual, haya salido
    /// bien o no: si otra terminal lo convirtió o anuló mientras tanto, la pantalla tiene que mostrar lo que hay en la base.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeAnular))]
    private async Task AnularAsync(PresupuestoFila? fila)
    {
        if (fila is null || !fila.PuedeAnular)
        {
            return;
        }

        if (!_dialogService.Confirmar(
                "Anular presupuesto",
                $"¿Anular el presupuesto N.º {fila.Numero}? No se podrá recuperar en el POS."))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await EjecutarEnAccesoAsync(token => _presupuestoService.AnularAsync(fila.IdPresupuesto, token));
        }
        catch (Exception ex)
        {
            ManejarError(ex);
        }

        await CargarPresupuestosAsync();
    }

    private static bool PuedeAnular(PresupuestoFila? fila)
    {
        return fila?.PuedeAnular == true;
    }

    /// <summary>
    /// Ejecuta una operación del servicio en la fila de accesos de la pantalla (nunca en paralelo con otra consulta
    /// sobre el mismo DbContext) y fuera del hilo de UI. Si falla, relanza la excepción original a quien llama.
    /// </summary>
    private async Task EjecutarEnAccesoAsync(Func<CancellationToken, Task> operacion)
    {
        Exception? error = null;

        await _carga.EjecutarOperacionAsync(
            async token =>
            {
                await Task.Run(() => operacion(token), token);
                return true;
            },
            _ => { },
            ex => error = ex);

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    /// <summary>
    /// Traduce cada error a lo que el usuario necesita saber. El orden importa: la transición inválida es una
    /// <see cref="DomainException"/>, y el conflicto de concurrencia no lo es.
    /// </summary>
    private void ManejarError(Exception ex)
    {
        switch (ex)
        {
            case TransicionPresupuestoInvalidaException transicion:
                _dialogService.MostrarAlerta(
                    "El presupuesto cambió de estado",
                    $"{transicion.Message} Otra terminal lo modificó: se actualizaron los datos.");
                break;

            case ConflictoDeConcurrenciaException:
                _dialogService.MostrarAlerta(
                    "Presupuesto modificado en otra terminal",
                    "Otra terminal modificó este presupuesto mientras lo revisaba. Se actualizaron los datos: revíselos y repita la operación.");
                break;

            case DomainException dominio:
                _dialogService.MostrarAlerta("No se pudo completar la operación", dominio.Message);
                break;

            default:
                _logger.LogError(ex, "Error inesperado al operar sobre un presupuesto");
                _dialogService.MostrarError(
                    "Error inesperado",
                    "No se pudo completar la operación. El detalle quedó registrado en el log.");
                break;
        }
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela lo pendiente para que no use el DbContext ya
    /// descartado (H-19).
    /// </summary>
    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}
