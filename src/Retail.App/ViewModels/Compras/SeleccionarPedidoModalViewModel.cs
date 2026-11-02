using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Compras;

/// <summary>
/// ViewModel para el diálogo modal de selección de pedidos emitidos a importar en compras (Track 4.3, P2.4).
/// Consume de forma estricta los contratos de frontera de IPedidoService (§5.2).
/// Todo acceso a datos pasa por CargaSerializada y Task.Run (Ley 4).
/// </summary>
public partial class SeleccionarPedidoModalViewModel : ObservableObject, IDisposable
{
    private readonly IPedidoService _pedidoService;
    private readonly ILogger<SeleccionarPedidoModalViewModel> _logger;
    private readonly CargaSerializada _carga;

    // Evita mostrar "no tiene pedidos" antes de que termine la primera carga.
    private bool _cargaIntentada;

    public int IdProveedor { get; }

    /// <summary>Nombre del proveedor, para que el diálogo diga de quién son los pedidos.</summary>
    public string RazonSocialProveedor { get; }

    public ObservableCollection<PedidoEmitidoDto> Pedidos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TienePedidoSeleccionado))]
    private PedidoEmitidoDto? _pedidoSeleccionado;

    /// <summary>Habilita "Recibir pedido": hay una fila elegida en la grilla.</summary>
    public bool TienePedidoSeleccionado => PedidoSeleccionado is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    /// <summary>La carga terminó sin error y el proveedor no tiene pedidos emitidos.</summary>
    public bool EstaVacio => _cargaIntentada && !IsBusy && MensajeError is null && Pedidos.Count == 0;

    public bool TieneMensajeError => !string.IsNullOrEmpty(MensajeError);

    public PedidoEmitidoDto? PedidoElegido { get; private set; }

    public bool OperacionConfirmada { get; private set; }

    public SeleccionarPedidoModalViewModel(
        int idProveedor,
        string razonSocialProveedor,
        IPedidoService pedidoService,
        ILogger<SeleccionarPedidoModalViewModel>? logger = null)
    {
        IdProveedor = idProveedor;
        RazonSocialProveedor = razonSocialProveedor ?? throw new ArgumentNullException(nameof(razonSocialProveedor));
        _pedidoService = pedidoService ?? throw new ArgumentNullException(nameof(pedidoService));
        _logger = logger ?? NullLogger<SeleccionarPedidoModalViewModel>.Instance;
        _carga = new CargaSerializada(_logger);
        Pedidos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EstaVacio));
    }

    [RelayCommand]
    public async Task CargarPedidosAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        MensajeError = null;

        await _carga.EjecutarAsync(
            token => Task.Run(() => _pedidoService.ListarEmitidosPorProveedorAsync(IdProveedor, token), token),
            resultados =>
            {
                Pedidos.Clear();
                if (resultados != null)
                {
                    foreach (var p in resultados)
                    {
                        Pedidos.Add(p);
                    }
                }
            },
            ex =>
            {
                _logger.LogError(ex, "Error al cargar pedidos emitidos para el proveedor {IdProveedor}", IdProveedor);
                MensajeError = "No se pudieron cargar los pedidos emitidos del proveedor.";
            },
            cancellationToken: cancellationToken);

        _cargaIntentada = true;
        IsBusy = _carga.EnCurso;
        OnPropertyChanged(nameof(EstaVacio));
    }

    [RelayCommand]
    public void SeleccionarPedido(PedidoEmitidoDto? pedido)
    {
        if (pedido is null)
        {
            return;
        }

        PedidoElegido = pedido;
        OperacionConfirmada = true;
    }

    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}

