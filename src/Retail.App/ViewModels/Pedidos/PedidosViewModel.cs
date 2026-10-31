using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Reportes;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Pedidos;

/// <summary>
/// Bandeja de pedidos a proveedor (IU-12, RF-29): filtros por estado y proveedor, grilla paginada en SQL y panel de
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
    }

    // ------------------------------------------------------------------ //
    // Filtros y grilla                                                    //
    // ------------------------------------------------------------------ //

    public IReadOnlyList<OpcionEstadoPedido> OpcionesEstado { get; } = OpcionEstadoPedido.Todas;

    public ObservableCollection<OpcionFiltro> Proveedores { get; } = new();

    public ObservableCollection<PedidoFila> Pedidos { get; } = new();

    [ObservableProperty]
    private OpcionEstadoPedido _estadoFiltro = OpcionEstadoPedido.Todas[0];

    [ObservableProperty]
    private OpcionFiltro? _proveedorFiltro;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    private int _totalPaginas = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _totalRegistros;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _tamanoPagina = 20;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalRegistros == 0
        ? "Sin pedidos"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistros)} de {TotalRegistros} pedidos";

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
    private PedidoDetalleDto? _detalle;

    public ObservableCollection<RenglonPedidoFila> Renglones { get; } = new();

    /// <summary>Motivo que el usuario escribe para cancelar el pedido seleccionado (G2).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelarCommand))]
    private string _motivoParaCancelar = string.Empty;

    public bool TieneDetalle => Detalle is not null;

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
        var consulta = new ConsultaPedidosDto
        {
            Estado = EstadoFiltro.Estado,
            IdProveedor = ProveedorFiltro?.Id,
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

    partial void OnEstadoFiltroChanged(OpcionEstadoPedido value)
    {
        RecargarDesdePrimeraPagina();
    }

    partial void OnProveedorFiltroChanged(OpcionFiltro? value)
    {
        RecargarDesdePrimeraPagina();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        RecargarDesdePrimeraPagina();
    }

    partial void OnPedidoSeleccionadoChanged(PedidoFila? value)
    {
        MotivoParaCancelar = string.Empty;

        if (value is null)
        {
            MostrarDetalle(null);
            return;
        }

        _ = CargarDetalleAsync(value.IdPedido);
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

    // ------------------------------------------------------------------ //
    // Comandos sobre el pedido seleccionado                               //
    // ------------------------------------------------------------------ //

    private bool PuedeEmitir() => EsBorrador && Detalle!.CantidadRenglones > 0;

    private bool PuedeCancelar() => PuedeCancelarse && !string.IsNullOrWhiteSpace(MotivoParaCancelar);

    [RelayCommand(CanExecute = nameof(PuedeEmitir))]
    private async Task EmitirAsync()
    {
        var pedido = Detalle!;
        if (!_dialogService.Confirmar(
                "Emitir pedido",
                $"¿Emitir el pedido #{pedido.IdPedido} a {pedido.RazonSocialProveedor}? Una vez emitido, sus renglones ya no se pueden modificar."))
        {
            return;
        }

        await EjecutarComandoAsync(
            token => _pedidoService.EmitirAsync(pedido.IdPedido, token));
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

    /// <summary>Guarda la cantidad editada de un renglón (G3): solo si la fila tiene un cambio válido.</summary>
    [RelayCommand]
    private async Task AplicarCantidadAsync(RenglonPedidoFila? fila)
    {
        if (fila is null || !fila.TieneCambios || Detalle is null)
        {
            return;
        }

        var dto = new CambiarCantidadRenglonDto
        {
            IdPedido = Detalle.IdPedido,
            IdCatalogoProveedor = fila.IdCatalogoProveedor,
            Cantidad = fila.Cantidad
        };

        await EjecutarComandoAsync(
            token => _pedidoService.CambiarCantidadAsync(dto, token));
    }

    [RelayCommand]
    private async Task QuitarRenglonAsync(RenglonPedidoFila? fila)
    {
        if (fila is null || !fila.EsEditable || Detalle is null)
        {
            return;
        }

        var idPedido = Detalle.IdPedido;
        if (!_dialogService.Confirmar("Quitar renglón", $"¿Quitar \"{fila.Descripcion}\" del pedido #{idPedido}?"))
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
        }
        else
        {
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
        Proveedores.Clear();
        Proveedores.Add(new OpcionFiltro(null, "Todos"));
        foreach (var proveedor in proveedores.OrderBy(p => p.RazonSocial))
        {
            Proveedores.Add(new OpcionFiltro(proveedor.IdProveedor, proveedor.RazonSocial));
        }

        ProveedorFiltro = Proveedores[0];
    }

    private void MostrarPedidos(PedidosPaginadosDto resultado)
    {
        // Tras recargar se conserva la selección si el pedido sigue en la página; la fila nueva vuelve a cargar su
        // detalle, así el panel muestra el estado actual.
        var idSeleccionado = PedidoSeleccionado?.IdPedido;

        Pedidos.Clear();
        foreach (var resumen in resultado.Items)
        {
            Pedidos.Add(new PedidoFila(resumen, _reloj));
        }

        TotalRegistros = resultado.TotalRegistros;
        TotalPaginas = Math.Max(1, resultado.TotalPaginas);
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));

        PedidoSeleccionado = Pedidos.FirstOrDefault(p => p.IdPedido == idSeleccionado);
    }

    private void MostrarDetalle(PedidoDetalleDto? detalle)
    {
        Detalle = detalle;
        Renglones.Clear();
        if (detalle is null)
        {
            return;
        }

        var editable = detalle.Estado == EstadoPedidoEnum.Borrador;
        foreach (var renglon in detalle.Renglones)
        {
            Renglones.Add(new RenglonPedidoFila(renglon, editable));
        }
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
