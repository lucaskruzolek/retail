using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Comun;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Bandeja de pedidos a proveedor (IU-12, RF-29): filtros de columna (estado y proveedor, selección múltiple), grilla paginada en SQL y panel de
/// detalle con las acciones que permite el estado del pedido (§4.1 del rector del Track 4.3). No navega a Compras
/// (A-12).
/// </summary>
public partial class PedidosViewModel : ObservableObject, IDisposable
{
    private readonly IPedidoService _pedidoService;
    private readonly IProveedorService _proveedorService;
    private readonly IPedidoDialogService _dialogService;
    private readonly TimeProvider _reloj;
    private readonly ILogger<PedidosViewModel> _logger;

    // Una sola fila de accesos para el DbContext de la pantalla (H-19). La grilla usa la carga reemplazable; el
    // detalle y los comandos, operaciones que ninguna carga nueva cancela (si el detalle fuera reemplazable, cada
    // selección cancelaría la recarga de la grilla y al revés).
    private readonly CargaSerializada _carga;

    // Evita que el primer valor de los filtros dispare una carga mientras la pantalla se inicializa.
    private bool _inicializando;

    // Verdadero mientras MostrarPedidos reemplaza la lista: la grilla suelta y vuelve a tomar la selección, y eso no es
    // el usuario eligiendo otro pedido.
    private bool _recargandoGrilla;

    // Verdadero mientras se devuelve la selección al pedido anterior porque el usuario no quiso descartar cantidades.
    private bool _revirtiendoSeleccion;

    // Un comando del pedido (guardar, quitar, emitir…) recarga todo: el detalle se vuelve a leer aunque haya cantidades
    // en edición, que el propio comando ya resolvió. Una recarga común (filtros, páginas) las conserva.
    private bool _forzarRecargaDetalle;

    // Los renglones del detalle cuyo cambio de cantidad escuchamos para habilitar "Guardar cambios".
    private readonly List<RenglonPedidoFila> _renglonesObservados = [];

    public PedidosViewModel(
        IPedidoService pedidoService,
        IProveedorService proveedorService,
        IPedidoDialogService dialogService,
        TimeProvider reloj,
        ILogger<PedidosViewModel> logger)
    {
        _pedidoService = pedidoService ?? throw new ArgumentNullException(nameof(pedidoService));
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _carga = new CargaSerializada(_logger);

        // Los estados son fijos; los proveedores llegan al inicializar la pantalla.
        FiltroEstado.CargarOpciones(
        [
            new OpcionFiltroColumna((int)EstadoPedidoEnum.Borrador, "Borradores"),
            new OpcionFiltroColumna((int)EstadoPedidoEnum.Emitido, "Emitidos"),
            new OpcionFiltroColumna((int)EstadoPedidoEnum.Recibido, "Recibidos"),
            new OpcionFiltroColumna((int)EstadoPedidoEnum.Cancelado, "Cancelados")
        ]);

        FiltroFecha = new FiltroFechaColumnaViewModel("Fecha", _reloj);

        FiltroEstado.Aplicado += AlAplicarFiltroDeColumna;
        FiltroProveedor.Aplicado += AlAplicarFiltroDeColumna;
        FiltroFecha.Aplicado += AlAplicarFiltroDeColumna;
    }

    // ------------------------------------------------------------------ //
    // Filtros y grilla                                                    //
    // ------------------------------------------------------------------ //

    /// <summary>Filtro de la columna Estado: los cuatro estados del pedido (vacío = todos).</summary>
    public FiltroColumnaViewModel FiltroEstado { get; } = new("Estado");

    /// <summary>Filtro de la columna Proveedor; sus opciones llegan al inicializar la pantalla.</summary>
    public FiltroColumnaViewModel FiltroProveedor { get; } = new("Proveedor");

    /// <summary>Filtro de la columna Fecha: un rango de días; la consulta lo convierte a UTC y lo resuelve en SQL.</summary>
    public FiltroFechaColumnaViewModel FiltroFecha { get; }

    public ObservableCollection<PedidoFila> Pedidos { get; } = new();

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

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
    private int _tamanoPagina = 20;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public bool HayFiltrosActivos =>
        !string.IsNullOrWhiteSpace(TextoBusqueda) ||
        FiltroEstado.HayFiltroActivo ||
        FiltroProveedor.HayFiltroActivo ||
        FiltroFecha.HayFiltroActivo;

    // ------------------------------------------------------------------ //
    // Selección y detalle                                                 //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private PedidoFila? _pedidoSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDetalle))]
    [NotifyPropertyChangedFor(nameof(EsBorrador))]
    [NotifyPropertyChangedFor(nameof(PuedeCancelarse))]
    [NotifyPropertyChangedFor(nameof(TieneMotivoCancelacion))]
    [NotifyPropertyChangedFor(nameof(FechaCreacionLocal))]
    [NotifyPropertyChangedFor(nameof(FechaEmisionLocal))]
    [NotifyPropertyChangedFor(nameof(FechaCierreLocal))]
    [NotifyCanExecuteChangedFor(nameof(EmitirCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelarCommand))]
    [NotifyCanExecuteChangedFor(nameof(GuardarCambiosCommand))]
    private PedidoDetalleDto? _detalle;

    public ObservableCollection<RenglonPedidoFila> Renglones { get; } = new();

    /// <summary>Motivo que el usuario escribe para cancelar el pedido seleccionado (G2).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelarCommand))]
    private string _motivoParaCancelar = string.Empty;

    public bool TieneDetalle => Detalle is not null;

    /// <summary>Hay cantidades editadas y todavía no guardadas: habilita "Guardar cambios".</summary>
    public bool HayCambiosSinGuardar => Renglones.Any(r => r.TieneCambios);

    private int CantidadesSinGuardar => Renglones.Count(r => r.TieneCambios);

    public bool EsBorrador => Detalle?.Estado == EstadoPedidoEnum.Borrador;

    /// <summary>Un borrador o un pedido emitido se pueden cancelar con motivo; uno recibido o cancelado, no.</summary>
    public bool PuedeCancelarse => Detalle?.Estado is EstadoPedidoEnum.Borrador or EstadoPedidoEnum.Emitido;

    public bool TieneMotivoCancelacion => !string.IsNullOrWhiteSpace(Detalle?.MotivoCancelacion);

    public DateTime? FechaCreacionLocal => ALocal(Detalle?.FechaCreacion);

    public DateTime? FechaEmisionLocal => ALocal(Detalle?.FechaEmision);

    public DateTime? FechaCierreLocal => ALocal(Detalle?.FechaCierre);

    // ------------------------------------------------------------------ //
    // Estado de la pantalla                                               //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    // ------------------------------------------------------------------ //
    // Carga                                                               //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Carga los proveedores del filtro y la primera página de pedidos. La invoca la vista al mostrarse.
    /// </summary>
    public async Task InicializarAsync()
    {
        _inicializando = true;
        try
        {
            await _carga.EjecutarOperacionAsync(
                token => Task.Run(() => _proveedorService.ListarProveedoresAsync(token), token),
                MostrarProveedores,
                ex =>
                {
                    _logger.LogError(ex, "Error al cargar los proveedores del filtro de pedidos");
                    MensajeError = "No se pudo cargar la lista de proveedores.";
                });
        }
        finally
        {
            _inicializando = false;
        }

        PaginaActual = 1;
        await CargarPedidosAsync();
    }

    public async Task CargarPedidosAsync(CancellationToken cancellationToken = default)
    {
        var (fechaDesdeUtc, fechaHastaUtc) = FiltroFecha.RangoUtc();
        var consulta = new ConsultaPedidosDto
        {
            TerminoBusqueda = TextoBusqueda,
            Estados = FiltroEstado.IdsAplicados.Select(id => (EstadoPedidoEnum)id).ToList(),
            IdsProveedor = FiltroProveedor.IdsAplicados,
            FechaDesdeUtc = fechaDesdeUtc,
            FechaHastaUtc = fechaHastaUtc,
            Pagina = PaginaActual,
            TamanoPagina = TamanoPagina
        };

        IsBusy = true;
        MensajeError = null;

        await _carga.EjecutarAsync(
            token => Task.Run(() => _pedidoService.ListarPedidosAsync(consulta, token), token),
            MostrarPedidos,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar los pedidos");
                MensajeError = "No se pudieron cargar los pedidos.";
            },
            cancellationToken: cancellationToken);

        IsBusy = _carga.EnCurso;
    }

    private void AlAplicarFiltroDeColumna(object? sender, EventArgs e)
    {
        ActualizarEstadoFiltros();
        RecargarDesdePrimeraPagina();
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        ActualizarEstadoFiltros();
        RecargarDesdePrimeraPagina();
    }

    [RelayCommand(CanExecute = nameof(HayFiltrosActivos))]
    public void LimpiarFiltros()
    {
        // Se borra el texto sin disparar su propia recarga: la pantalla consulta una sola vez, al final.
        _inicializando = true;
        try
        {
            TextoBusqueda = string.Empty;
        }
        finally
        {
            _inicializando = false;
        }

        FiltroEstado.QuitarFiltro();
        FiltroProveedor.QuitarFiltro();
        FiltroFecha.QuitarFiltro();
        ActualizarEstadoFiltros();
        RecargarDesdePrimeraPagina();
    }

    private void ActualizarEstadoFiltros()
    {
        OnPropertyChanged(nameof(HayFiltrosActivos));
        LimpiarFiltrosCommand.NotifyCanExecuteChanged();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        RecargarDesdePrimeraPagina();
    }

    partial void OnPedidoSeleccionadoChanged(PedidoFila? oldValue, PedidoFila? newValue)
    {
        var forzar = _forzarRecargaDetalle;
        _forzarRecargaDetalle = false;

        if (!_revirtiendoSeleccion && !forzar && HayCambiosSinGuardar)
        {
            // Una recarga de la lista (filtros, páginas) suelta y vuelve a tomar la selección: las cantidades en edición
            // se conservan. Solo el usuario eligiendo otro pedido las descarta, y antes se le pregunta.
            if (_recargandoGrilla)
            {
                return;
            }

            if (!ConfirmarDescarteDeCantidades())
            {
                _revirtiendoSeleccion = true;
                try
                {
                    PedidoSeleccionado = oldValue;
                }
                finally
                {
                    _revirtiendoSeleccion = false;
                }

                return;
            }
        }

        MotivoParaCancelar = string.Empty;

        if (newValue is null)
        {
            MostrarDetalle(null);
            return;
        }

        _ = CargarDetalleAsync(newValue.IdPedido);
    }

    /// <summary>Pregunta si se descartan las cantidades sin guardar; el usuario puede quedarse a guardarlas.</summary>
    private bool ConfirmarDescarteDeCantidades()
    {
        return _dialogService.Confirmar(
            "Cantidades sin guardar",
            $"Hay {CantidadesSinGuardar} cambio(s) de cantidad sin guardar en este pedido. Si continúa, se descartarán. ¿Desea continuar?");
    }

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarPedidosAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarPedidosAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarPedidosAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarPedidosAsync();
        }
    }

    /// <summary>Va a una página puntual (los números de la barra de paginación); fuera de rango no hace nada.</summary>
    [RelayCommand]
    public async Task IrAPaginaAsync(int pagina)
    {
        if (pagina >= 1 && pagina <= TotalPaginas && pagina != PaginaActual)
        {
            PaginaActual = pagina;
            await CargarPedidosAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Comandos sobre el pedido seleccionado                               //
    // ------------------------------------------------------------------ //

    private bool PuedeEmitir() => EsBorrador && Detalle!.CantidadRenglones > 0;

    private bool PuedeCancelar() => PuedeCancelarse && !string.IsNullOrWhiteSpace(MotivoParaCancelar);

    /// <summary>
    /// Emite el pedido tal como se ve en pantalla: si hay cantidades sin guardar, las guarda primero (todas o ninguna) y
    /// después emite. Emitir es irreversible; no puede actuar sobre lo que hay en la base si el usuario ve otra cosa.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeEmitir))]
    private async Task EmitirAsync()
    {
        var pedido = Detalle!;
        var cambios = HayCambiosSinGuardar ? CrearCambioDeCantidades(pedido.IdPedido) : null;
        var guardado = cambios is null ? string.Empty : $"Se guardarán {cambios.Renglones.Count} cambio(s) de cantidad y se emitirá el pedido. ";

        if (!_dialogService.Confirmar(
                "Emitir pedido",
                $"{guardado}¿Emitir el pedido #{pedido.IdPedido} a {pedido.RazonSocialProveedor}? Una vez emitido, sus renglones ya no se pueden modificar."))
        {
            return;
        }

        await EjecutarComandoAsync(
            async token =>
            {
                // Si el guardado falla (validación, concurrencia), no se emite: la excepción corta acá
                if (cambios is not null)
                {
                    await _pedidoService.CambiarCantidadesAsync(cambios, token);
                }

                await _pedidoService.EmitirAsync(pedido.IdPedido, token);
            });
    }

    [RelayCommand(CanExecute = nameof(PuedeCancelar))]
    private async Task CancelarAsync()
    {
        var pedido = Detalle!;
        var motivo = MotivoParaCancelar.Trim();
        if (!_dialogService.Confirmar(
                "Cancelar pedido",
                $"¿Cancelar el pedido #{pedido.IdPedido} a {pedido.RazonSocialProveedor}?\n\nMotivo: {motivo}\n\nLa cancelación no se puede deshacer."))
        {
            return;
        }

        await EjecutarComandoAsync(
            token => _pedidoService.CancelarAsync(new CancelarPedidoDto { IdPedido = pedido.IdPedido, Motivo = motivo }, token));
    }

    /// <summary>
    /// Guarda de una vez todas las cantidades editadas del borrador (G3): todas o ninguna, en una sola transacción. Lo
    /// invocan el botón "Guardar cambios" y Enter en cualquier cantidad.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HayCambiosSinGuardar))]
    private async Task GuardarCambiosAsync()
    {
        if (Detalle is null)
        {
            return;
        }

        var cambios = CrearCambioDeCantidades(Detalle.IdPedido);

        await EjecutarComandoAsync(
            token => _pedidoService.CambiarCantidadesAsync(cambios, token));
    }

    private CambiarCantidadesPedidoDto CrearCambioDeCantidades(int idPedido)
    {
        return new CambiarCantidadesPedidoDto
        {
            IdPedido = idPedido,
            Renglones = Renglones
                .Where(r => r.TieneCambios)
                .Select(r => new CantidadRenglonDto { IdCatalogoProveedor = r.IdCatalogoProveedor, Cantidad = r.Cantidad })
                .ToList()
        };
    }

    [RelayCommand]
    private async Task QuitarRenglonAsync(RenglonPedidoFila? fila)
    {
        if (fila is null || !fila.EsEditable || Detalle is null)
        {
            return;
        }

        var idPedido = Detalle.IdPedido;
        var descarte = HayCambiosSinGuardar ? $"\n\nLos {CantidadesSinGuardar} cambio(s) de cantidad sin guardar se descartarán." : string.Empty;
        if (!_dialogService.Confirmar("Quitar renglón", $"¿Quitar \"{fila.Descripcion}\" del pedido #{idPedido}?{descarte}"))
        {
            return;
        }

        await EjecutarComandoAsync(
            token => _pedidoService.QuitarRenglonAsync(idPedido, fila.IdCatalogoProveedor, token));
    }

    /// <summary>
    /// Ejecuta un comando del servicio en la fila de accesos de la pantalla y después recarga, haya salido bien o
    /// no: tras un error de estado o de concurrencia, la pantalla tiene que mostrar lo que hay realmente en la base.
    /// La única excepción es un rechazo de validación, que no tocó la base: recargar solo borraría su mensaje.
    /// </summary>
    private async Task EjecutarComandoAsync(Func<CancellationToken, Task> comando)
    {
        IsBusy = true;
        MensajeError = null;
        var recargar = true;
        _forzarRecargaDetalle = true;

        await _carga.EjecutarOperacionAsync(
            async token =>
            {
                await Task.Run(() => comando(token), token);
                return true;
            },
            _ => { },
            ex => recargar = ManejarError(ex));

        if (recargar)
        {
            await CargarPedidosAsync();

            // Si la recarga no llegó a cambiar la selección (se canceló o falló), la marca no debe quedar armada
            _forzarRecargaDetalle = false;
        }
        else
        {
            // No se recargó: lo que el usuario escribió sigue en pantalla
            _forzarRecargaDetalle = false;
            IsBusy = _carga.EnCurso;
        }
    }

    /// <summary>
    /// Traduce cada error a lo que el usuario necesita saber y devuelve si hay que recargar la pantalla. El orden
    /// importa: la transición inválida es una <see cref="DomainException"/>, y el conflicto de concurrencia no lo es.
    /// </summary>
    private bool ManejarError(Exception ex)
    {
        switch (ex)
        {
            case TransicionPedidoInvalidaException transicion:
                _dialogService.MostrarAlerta(
                    "El pedido cambió de estado",
                    $"{transicion.Message} Otra terminal lo modificó: se actualizaron los datos.");
                return true;

            case ConflictoDeConcurrenciaException:
                _dialogService.MostrarAlerta(
                    "Pedido modificado en otra terminal",
                    "Otra terminal modificó este pedido mientras lo revisaba. Se actualizaron los datos: revíselos y repita la operación.");
                return true;

            case ValidationException validacion:
                MensajeError = string.Join(" ", validacion.Errors.Select(e => e.ErrorMessage));
                return false;

            case DomainException dominio:
                _dialogService.MostrarAlerta("No se pudo completar la operación", dominio.Message);
                return true;

            default:
                _logger.LogError(ex, "Error inesperado al operar sobre un pedido");
                _dialogService.MostrarError(
                    "Error inesperado",
                    "No se pudo completar la operación. El detalle quedó registrado en el log.");
                return true;
        }
    }

    private void RecargarDesdePrimeraPagina()
    {
        if (_inicializando)
        {
            return;
        }

        PaginaActual = 1;
        _ = CargarPedidosAsync();
    }

    private async Task CargarDetalleAsync(int idPedido)
    {
        await _carga.EjecutarOperacionAsync(
            token => Task.Run(() => _pedidoService.ObtenerDetalleAsync(idPedido, token), token),
            detalle =>
            {
                // Si el usuario ya eligió otra fila, este detalle llegó tarde y no se muestra.
                if (PedidoSeleccionado?.IdPedido == idPedido)
                {
                    MostrarDetalle(detalle);
                }
            },
            ex =>
            {
                _logger.LogError(ex, "Error al cargar el detalle del pedido {IdPedido}", idPedido);
                MensajeError = "No se pudo cargar el detalle del pedido.";
            });
    }

    private void MostrarProveedores(IReadOnlyList<ProveedorDto> proveedores)
    {
        FiltroProveedor.CargarOpciones(
            proveedores
                .OrderBy(p => p.RazonSocial)
                .Select(p => new OpcionFiltroColumna(p.IdProveedor, p.RazonSocial)));
    }

    private void MostrarPedidos(PedidosPaginadosDto resultado)
    {
        // Tras recargar se conserva la selección si el pedido sigue en la página; la fila nueva vuelve a cargar su
        // detalle, así el panel muestra el estado actual.
        var idSeleccionado = PedidoSeleccionado?.IdPedido;

        _recargandoGrilla = true;
        try
        {
            Pedidos.Clear();
            foreach (var resumen in resultado.Items)
            {
                Pedidos.Add(new PedidoFila(resumen, _reloj));
            }

            TotalRegistros = resultado.TotalRegistros;
            TotalPaginas = Math.Max(1, resultado.TotalPaginas);
            OnPropertyChanged(nameof(PuedeAvanzarPagina));

            PedidoSeleccionado = Pedidos.FirstOrDefault(p => p.IdPedido == idSeleccionado);
        }
        finally
        {
            _recargandoGrilla = false;
        }

        // Si el pedido que se editaba salió de la página, su detalle (y lo escrito) ya no corresponde a ninguna fila
        if (PedidoSeleccionado is null && Detalle is not null)
        {
            MostrarDetalle(null);
        }
    }

    private void MostrarDetalle(PedidoDetalleDto? detalle)
    {
        Detalle = detalle;

        foreach (var observado in _renglonesObservados)
        {
            observado.PropertyChanged -= AlCambiarRenglon;
        }

        _renglonesObservados.Clear();
        Renglones.Clear();
        if (detalle is not null)
        {
            var editable = detalle.Estado == EstadoPedidoEnum.Borrador;
            foreach (var renglon in detalle.Renglones)
            {
                var fila = new RenglonPedidoFila(renglon, editable);
                fila.PropertyChanged += AlCambiarRenglon;
                _renglonesObservados.Add(fila);
                Renglones.Add(fila);
            }
        }

        NotificarCambiosSinGuardar();
    }

    private void AlCambiarRenglon(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RenglonPedidoFila.TieneCambios))
        {
            NotificarCambiosSinGuardar();
        }
    }

    private void NotificarCambiosSinGuardar()
    {
        OnPropertyChanged(nameof(HayCambiosSinGuardar));
        GuardarCambiosCommand.NotifyCanExecuteChanged();
    }

    private DateTime? ALocal(DateTime? fechaUtc)
    {
        return fechaUtc is { } fecha ? TimeZoneInfo.ConvertTimeFromUtc(fecha, _reloj.LocalTimeZone) : null;
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
