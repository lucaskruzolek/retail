using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Compras;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Compras;

/// <summary>
/// ViewModel principal para la gestión de compras a distribuidores e ingreso de facturas/remitos (Módulo 4.2).
/// Todo acceso a datos pasa por <see cref="CargaSerializada"/> (H-19, Ley 4).
/// Las excepciones de dominio y de concurrencia se muestran como alertas; solo los errores inesperados van al log.
/// Soporta la importación de pedidos emitidos (Track 4.3, P2.4).
/// </summary>
public partial class ComprasViewModel : ObservableObject, IDisposable
{
    // C-5 (H-19): Serializa todos los accesos a datos de esta pantalla sobre el mismo DbContext y
    // cancela las cargas en curso al salir de ella.
    private readonly CargaSerializada _carga;

    private readonly IProveedorService _proveedorService;
    private readonly ICompraService _compraService;
    private readonly IPedidoService? _pedidoService;
    private readonly ICompraDialogService? _dialogService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ILogger<ComprasViewModel> _logger;

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();
    public ObservableCollection<DetalleCompraItemViewModel> DetalleCompraItems { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportarPedidoCommand))]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    private string _numeroComprobante = string.Empty;

    [ObservableProperty]
    private string _tipoComprobante = "FACTURA";

    [ObservableProperty]
    private DateTime _fechaEmision = DateTime.Today;

    [ObservableProperty]
    private int? _idPedidoOrigen;

    [ObservableProperty]
    private string? _informacionPedidoOrigen;

    [ObservableProperty]
    private bool _tienePedidoOrigen;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    /// <summary>
    /// Indica si se puede invocar la acción de importar pedido (requiere proveedor seleccionado).
    /// </summary>
    public bool PuedeImportarPedido => ProveedorSeleccionado != null && ProveedorSeleccionado.IdProveedor > 0;

    /// <summary>
    /// Total general de la factura de compra acumulando los subtotales de cada renglón.
    /// </summary>
    public decimal TotalCompra => DetalleCompraItems.Sum(i => i.Subtotal);

    public ComprasViewModel(
        IProveedorService proveedorService,
        ICompraService compraService,
        IPedidoService pedidoService,
        ICompraDialogService dialogService,
        ICurrentUserSession currentUserSession,
        ILogger<ComprasViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _compraService = compraService ?? throw new ArgumentNullException(nameof(compraService));
        _pedidoService = pedidoService ?? throw new ArgumentNullException(nameof(pedidoService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _carga = new CargaSerializada(_logger);
    }

    /// <summary>
    /// Constructor de compatibilidad para pruebas unitarias focales de compras independientes de pedidos.
    /// </summary>
    public ComprasViewModel(
        IProveedorService proveedorService,
        ICompraService compraService,
        ICurrentUserSession currentUserSession,
        ILogger<ComprasViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _compraService = compraService ?? throw new ArgumentNullException(nameof(compraService));
        _pedidoService = null;
        _dialogService = null;
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _carga = new CargaSerializada(_logger);
    }

    partial void OnProveedorSeleccionadoChanged(ProveedorDto? value)
    {
        OnPropertyChanged(nameof(PuedeImportarPedido));
    }

    [RelayCommand]
    private async Task CargarProveedoresAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        await _carga.EjecutarAsync(
            token => Task.Run(() => _proveedorService.ListarProveedoresAsync(token), token),
            lista =>
            {
                Proveedores.Clear();
                if (lista != null)
                {
                    foreach (var p in lista)
                    {
                        Proveedores.Add(p);
                    }
                }
            },
            ex =>
            {
                _logger.LogError(ex, "Error al cargar proveedores en el módulo de compras");
                MensajeError = "No se pudieron cargar los proveedores.";
            },
            cancellationToken: cancellationToken);

        IsBusy = _carga.EnCurso;
    }

    [RelayCommand]
    private void AgregarItem()
    {
        // Aquí se integrará la apertura del diálogo de selección de artículos del catálogo
        MensajeEstado = "Acción de agregar artículo (F2) invocada.";
    }

    [RelayCommand(CanExecute = nameof(PuedeImportarPedido))]
    private async Task ImportarPedidoAsync()
    {
        if (ProveedorSeleccionado is null || _dialogService is null || _pedidoService is null)
        {
            return;
        }

        if (DetalleCompraItems.Count > 0)
        {
            var confirmar = _dialogService.Confirmar(
                "Importar pedido",
                "La factura de compra ya contiene artículos cargados.\n¿Desea reemplazarlos con los del pedido seleccionado?");

            if (!confirmar)
            {
                return;
            }
        }

        var pedidoElegido = await _dialogService.MostrarSeleccionarPedidoModalAsync(ProveedorSeleccionado.IdProveedor);
        if (pedidoElegido is null)
        {
            return;
        }

        IsBusy = true;
        MensajeError = null;
        MensajeEstado = null;
        PedidoParaCompraDto? pedidoParaCompra = null;

        await _carga.EjecutarAsync(
            token => Task.Run(() => _pedidoService.ObtenerParaCompraAsync(pedidoElegido.IdPedido, token), token),
            resultado => pedidoParaCompra = resultado,
            ex =>
            {
                _logger.LogError(ex, "Error al obtener el pedido {IdPedido} para compra", pedidoElegido.IdPedido);
                MensajeError = "Error al obtener los detalles del pedido seleccionado.";
            });

        IsBusy = _carga.EnCurso;

        if (pedidoParaCompra is null)
        {
            _dialogService.MostrarAlerta(
                "Pedido no disponible",
                "El pedido seleccionado ya no está disponible (fue cancelado o recibido en otra terminal).");
            MensajeError = "El pedido seleccionado ya no está disponible.";
            return;
        }

        DetalleCompraItems.Clear();
        foreach (var renglon in pedidoParaCompra.Renglones)
        {
            DetalleCompraItems.Add(new DetalleCompraItemViewModel
            {
                IdArticulo = renglon.IdArticulo,
                CodigoArticulo = !string.IsNullOrWhiteSpace(renglon.CodigoProveedor)
                    ? renglon.CodigoProveedor
                    : (!string.IsNullOrWhiteSpace(renglon.CodigoBarras) ? renglon.CodigoBarras : renglon.IdArticulo.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                DescripcionArticulo = renglon.DescripcionArticulo,
                Cantidad = renglon.Cantidad,
                CostoUnitario = renglon.CostoPactado,
            });
        }

        IdPedidoOrigen = pedidoParaCompra.IdPedido;
        InformacionPedidoOrigen = $"Pedido #{pedidoParaCompra.IdPedido}";
        TienePedidoOrigen = true;
        OnPropertyChanged(nameof(TotalCompra));

        MensajeEstado = $"Pedido #{pedidoParaCompra.IdPedido} importado con {DetalleCompraItems.Count} renglón(es). Puede ajustar cantidades y costos a la factura.";
    }

    [RelayCommand]
    private void DesvincularPedido()
    {
        IdPedidoOrigen = null;
        InformacionPedidoOrigen = null;
        TienePedidoOrigen = false;
        MensajeEstado = "Pedido desvinculado. La compra ahora es manual (los artículos se conservan).";
    }

    [RelayCommand]
    private async Task RegistrarCompraAsync()
    {
        if (ProveedorSeleccionado is null)
        {
            MensajeError = "Debe seleccionar un proveedor o distribuidor para la compra.";
            return;
        }

        if (DetalleCompraItems.Count == 0)
        {
            MensajeError = "La factura de compra debe contener al menos un renglón de artículo.";
            return;
        }

        // Guard: la sesión podría haberse cerrado entre la carga de la pantalla y el click de confirmar.
        var idUsuario = _currentUserSession.IdUsuario;
        if (idUsuario is null)
        {
            MensajeError = "No hay una sesión de usuario activa. Por favor, inicie sesión nuevamente.";
            return;
        }

        IsBusy = true;
        MensajeError = null;
        MensajeEstado = null;

        try
        {
            var dto = new CrearCompraDto
            {
                IdProveedor = ProveedorSeleccionado.IdProveedor,
                IdUsuario = idUsuario.Value,
                IdPedidoOrigen = IdPedidoOrigen,
                TipoComprobante = TipoComprobante.Trim(),
                NumeroComprobante = NumeroComprobante.Trim(),
                FechaEmision = FechaEmision.ToUniversalTime(),
                Items = DetalleCompraItems
                    .Select(i => new DetalleCompraDto
                    {
                        IdArticulo = i.IdArticulo,
                        Cantidad = i.Cantidad,
                        CostoUnitario = i.CostoUnitario,
                    })
                    .ToList()
                    .AsReadOnly(),
            };

            // C-5 (Ley 4): toda operación pesada fuera del UI Dispatcher, a través de CargaSerializada.
            // Para operaciones mutables se usa EjecutarOperacionAsync (no reemplazable, no cancelable por otra carga).
            CompraResponseDto? resultado = null;
            await _carga.EjecutarOperacionAsync(
                token => Task.Run(() => _compraService.RegistrarCompraAsync(dto, token), token),
                r => resultado = r,
                ex =>
                {
                    // C-5: Clasificar la excepción para dar el mensaje adecuado al operador.
                    switch (ex)
                    {
                        case FluentValidation.ValidationException ve:
                            // Datos mal formados (frontera de entrada).
                            MensajeError = string.Join(" | ", ve.Errors.Select(e => e.ErrorMessage));
                            _logger.LogWarning("Validación fallida al registrar compra: {Errores}", MensajeError);
                            break;

                        case DomainException de:
                            // C-5: Regla de negocio (comprobante duplicado, artículo de baja, etc.).
                            // Es información para el operador; la grilla se conserva para reintentar.
                            MensajeError = de.Message;
                            break;

                        case ConflictoDeConcurrenciaException ce:
                            // C-5: Otra terminal modificó los mismos artículos (row_version, D-12).
                            // La grilla se conserva para reintentar.
                            MensajeError = ce.Message;
                            break;

                        default:
                            // C-5: Error inesperado: se registra en el log y se muestra un mensaje genérico.
                            _logger.LogError(ex, "Error al registrar la compra a proveedor");
                            MensajeError = "Error inesperado al registrar la compra. Intente de nuevo.";
                            break;
                    }
                });

            if (resultado is null)
            {
                // La carga fue cancelada (pantalla cerrada antes de completar) o falló.
                return;
            }

            LimpiarFormulario();

            MensajeEstado = $"Compra {resultado.TipoComprobante} Nro. {resultado.NumeroComprobante} registrada. " +
                            $"Total: {resultado.Total:C2}. " +
                            $"Artículos actualizados: {resultado.ArticulosActualizados}.";
        }
        catch (Exception ex)
        {
            // Defensa ante excepciones fuera de CargaSerializada (ej.: la excepción de sesión o de guard).
            _logger.LogError(ex, "Error inesperado antes de registrar la compra");
            MensajeError = "Error inesperado. Intente de nuevo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LimpiarFormulario()
    {
        DetalleCompraItems.Clear();
        NumeroComprobante = string.Empty;
        ProveedorSeleccionado = null;
        FechaEmision = DateTime.Today;
        IdPedidoOrigen = null;
        InformacionPedidoOrigen = null;
        TienePedidoOrigen = false;
        OnPropertyChanged(nameof(TotalCompra));
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela la carga en curso para que no use el
    /// DbContext ya descartado ni muestre errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}
