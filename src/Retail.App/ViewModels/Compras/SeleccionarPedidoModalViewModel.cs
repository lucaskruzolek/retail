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

    public int IdProveedor { get; }

    public ObservableCollection<PedidoEmitidoDto> Pedidos { get; } = new();

    [ObservableProperty]
    private PedidoEmitidoDto? _pedidoSeleccionado;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeError;

    public PedidoEmitidoDto? PedidoElegido { get; private set; }

    public bool OperacionConfirmada { get; private set; }

    public SeleccionarPedidoModalViewModel(
        int idProveedor,
        IPedidoService pedidoService,
        ILogger<SeleccionarPedidoModalViewModel>? logger = null)
    {
        IdProveedor = idProveedor;
        _pedidoService = pedidoService ?? throw new ArgumentNullException(nameof(pedidoService));
        _logger = logger ?? NullLogger<SeleccionarPedidoModalViewModel>.Instance;
        _carga = new CargaSerializada(_logger);
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

        IsBusy = _carga.EnCurso;
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

