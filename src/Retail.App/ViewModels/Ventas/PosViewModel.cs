using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// ViewModel principal para el Punto de Venta (POS) y terminal de mostrador (RF-09, RF-10, RNF-01).
/// Orquesta el SearchBar unificado con popup predictivo, la grilla del ticket contable,
/// atajos F1 a F12 y el despacho hacia el checkout multimedio.
/// </summary>
public partial class PosViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    private readonly IVentaService _ventaService;
    private readonly ICajaService _cajaService;
    private readonly ICurrentUserSession _session;
    private readonly IVentaDialogService _dialogService;
    private readonly ILogger<PosViewModel> _logger;

    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private bool _mostrarPopupBusqueda;

    [ObservableProperty]
    private int _indiceResultadoSeleccionado = -1;

    [ObservableProperty]
    private ClienteDto? _cliente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeCobrar))]
    private TurnoCajaDto? _turnoActivo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeCobrar))]
    private bool _cajaAbierta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    [NotifyPropertyChangedFor(nameof(TotalFormateado))]
    private decimal _descuento;

    [ObservableProperty]
    private ItemVentaPosViewModel? _itemSeleccionado;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ArticuloVentaDto> ResultadosBusqueda { get; } = new();

    public ObservableCollection<ItemVentaPosViewModel> Items { get; } = new();

    public string ClienteNombre => Cliente?.RazonSocialONombre ?? "Consumidor Final";

    public string ClienteDocumento => Cliente != null
        ? $"{Cliente.TipoDocumento}: {Cliente.NumeroDocumento}"
        : "Sin registrar";

    public bool ClienteTieneCtaCte => Cliente is { TieneCuentaCorriente: true };

    public decimal ClienteCreditoDisponible => Cliente == null
        ? 0m
        : Math.Max(0m, Cliente.LimiteCredito - Cliente.SaldoCuentaCorriente);

    public string ClienteCreditoDisponibleFormateado => ClienteCreditoDisponible.ToString("C2", CulturaArgentina);

    public int CantidadTotalArticulos => Items.Sum(i => i.Cantidad);

    public decimal Subtotal => Items.Sum(i => i.SubtotalItem);

    public decimal Total => Math.Max(0m, Subtotal - Descuento);

    public string SubtotalFormateado => Subtotal.ToString("C2", CulturaArgentina);

    public string DescuentoFormateado => Descuento.ToString("C2", CulturaArgentina);

    public string TotalFormateado => Total.ToString("C2", CulturaArgentina);

    public bool PuedeCobrar => Items.Count > 0 && CajaAbierta;

    public string OperadorNombre => _session.NombreCompleto;

    public PosViewModel(
        IVentaService ventaService,
        ICajaService cajaService,
        ICurrentUserSession session,
        IVentaDialogService dialogService,
        ILogger<PosViewModel>? logger = null)
    {
        _ventaService = ventaService ?? throw new ArgumentNullException(nameof(ventaService));
        _cajaService = cajaService ?? throw new ArgumentNullException(nameof(cajaService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? NullLogger<PosViewModel>.Instance;

        Items.CollectionChanged += (_, _) => RecalcularTotales();

        _ = CargarEstadoCajaAsync();
    }

    partial void OnClienteChanged(ClienteDto? value)
    {
        OnPropertyChanged(nameof(ClienteNombre));
        OnPropertyChanged(nameof(ClienteDocumento));
        OnPropertyChanged(nameof(ClienteTieneCtaCte));
        OnPropertyChanged(nameof(ClienteCreditoDisponible));
        OnPropertyChanged(nameof(ClienteCreditoDisponibleFormateado));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        var token = _searchCts.Token;
        string texto = value.Trim();

        if (texto.Length < 2)
        {
            ResultadosBusqueda.Clear();
            MostrarPopupBusqueda = false;
            IndiceResultadoSeleccionado = -1;
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(180, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var lista = new List<ArticuloVentaDto>();

                // Si son dígitos, intentamos código de barras exacto primero
                if (texto.All(char.IsDigit))
                {
                    var artCodigo = await _ventaService.BuscarPorCodigoBarrasAsync(texto, token);
                    if (artCodigo != null)
                    {
                        lista.Add(artCodigo);
                    }
                }

                // Luego búsqueda predictiva de texto
                var coincidenciasTexto = await _ventaService.BuscarPorTextoAsync(texto, 10, token);
                foreach (var art in coincidenciasTexto)
                {
                    if (lista.All(a => a.IdArticulo != art.IdArticulo))
                    {
                        lista.Add(art);
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        ResultadosBusqueda.Clear();
                        foreach (var item in lista)
                        {
                            ResultadosBusqueda.Add(item);
                        }

                        MostrarPopupBusqueda = ResultadosBusqueda.Count > 0;
                        IndiceResultadoSeleccionado = ResultadosBusqueda.Count > 0 ? 0 : -1;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Tarea cancelada normalmente por nueva tecla
            }
            catch (Exception ex) when (token.IsCancellationRequested ||
                                       ex.InnerException is OperationCanceledException ||
                                       ex.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
            {
                // Comando cancelado normalmente por nueva tecla (TDS Attention)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la búsqueda predictiva de artículos para mostrador.");
            }
        }, token);
    }

    [RelayCommand]
    public async Task ProcesarEnterAsync()
    {
        _searchCts?.Cancel();

        // 1. Si hay un ítem resaltado en el popup desplegado, cargarlo
        if (MostrarPopupBusqueda && IndiceResultadoSeleccionado >= 0 && IndiceResultadoSeleccionado < ResultadosBusqueda.Count)
        {
            var seleccionado = ResultadosBusqueda[IndiceResultadoSeleccionado];
            AgregarArticuloAlTicket(seleccionado);
            LimpiarBuscador();
            return;
        }

        // 2. Si se ingresó un valor directo en el campo sin navegar el popup
        string entrada = TextoBusqueda.Trim();
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return;
        }

        LimpiarBuscador();

        var articulo = await _ventaService.BuscarArticuloParaVentaAsync(entrada);
        if (articulo != null)
        {
            AgregarArticuloAlTicket(articulo);
        }
        else
        {
            _dialogService.MostrarAlerta(
                "Artículo no encontrado",
                $"No se encontró ningún artículo que coincida con '{entrada}'.");
        }
    }

    [RelayCommand]
    public void MoverSeleccionPopupArriba()
    {
        if (ResultadosBusqueda.Count == 0)
        {
            return;
        }

        if (IndiceResultadoSeleccionado > 0)
        {
            IndiceResultadoSeleccionado--;
        }
        else
        {
            IndiceResultadoSeleccionado = ResultadosBusqueda.Count - 1;
        }
    }

    [RelayCommand]
    public void MoverSeleccionPopupAbajo()
    {
        if (ResultadosBusqueda.Count == 0)
        {
            return;
        }

        if (IndiceResultadoSeleccionado < ResultadosBusqueda.Count - 1)
        {
            IndiceResultadoSeleccionado++;
        }
        else
        {
            IndiceResultadoSeleccionado = 0;
        }
    }

    [RelayCommand]
    public void CerrarPopupBusqueda()
    {
        LimpiarBuscador();
    }

    [RelayCommand]
    public void IncrementarCantidad(ItemVentaPosViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        item.Cantidad++;
        RecalcularTotales();
    }

    [RelayCommand]
    public void DecrementarCantidad(ItemVentaPosViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        if (item.Cantidad > 1)
        {
            item.Cantidad--;
            RecalcularTotales();
        }
        else
        {
            EliminarItem(item);
        }
    }

    [RelayCommand]
    public void EliminarItem(ItemVentaPosViewModel? item)
    {
        var target = item ?? ItemSeleccionado;
        if (target == null)
        {
            return;
        }

        Items.Remove(target);
        ReenumerarItems();
        RecalcularTotales();
    }

    [RelayCommand]
    public async Task SeleccionarClienteAsync()
    {
        var nuevoCliente = await _dialogService.MostrarSeleccionarClienteModalAsync(Cliente);
        Cliente = nuevoCliente;
    }

    [RelayCommand]
    public void LimpiarVenta()
    {
        if (Items.Count == 0)
        {
            return;
        }

        bool confirma = _dialogService.Confirmar(
            "Cancelar Venta",
            "¿Está seguro de que desea cancelar la venta actual y vaciar el mostrador?");

        if (confirma)
        {
            Items.Clear();
            Descuento = 0m;
            Cliente = null;
            RecalcularTotales();
        }
    }

    [RelayCommand]
    public async Task CobrarVentaAsync()
    {
        if (!PuedeCobrar)
        {
            if (!CajaAbierta)
            {
                _dialogService.MostrarAlerta(
                    "Caja Cerrada",
                    "El turno de caja se encuentra cerrado. Debe realizar la apertura para habilitar ventas (RF-13).");
            }
            return;
        }

        var pagos = await _dialogService.MostrarCobroModalAsync(Total, Cliente);
        if (pagos == null || pagos.Count == 0)
        {
            // Operación cancelada por el cajero
            return;
        }

        try
        {
            IsBusy = true;

            var crearVentaDto = new CrearVentaDto
            {
                IdTurno = TurnoActivo?.IdTurno ?? 1,
                IdUsuario = _session.IdUsuario ?? 1,
                IdCliente = Cliente?.IdCliente,
                Descuento = Descuento,
                Items = Items.Select(i => new DetalleVentaDto
                {
                    IdArticulo = i.IdArticulo,
                    CodigoBarras = i.CodigoBarras,
                    Descripcion = i.Descripcion,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario
                }).ToList(),
                Pagos = pagos
            };

            await _ventaService.RegistrarVentaAsync(crearVentaDto);

            // Venta completada con éxito: limpiar mostrador
            Items.Clear();
            Descuento = 0m;
            Cliente = null;
            RecalcularTotales();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar la venta en mostrador: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error de Cobro", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CargarEstadoCajaAsync()
    {
        try
        {
            TurnoActivo = await _cajaService.ObtenerTurnoActivoAsync();
            CajaAbierta = TurnoActivo?.Estado == EstadoTurnoEnum.Abierto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar el turno de caja activo: {Mensaje}", ex.Message);
            CajaAbierta = false;
        }
    }

    public void AgregarArticuloAlTicket(ArticuloVentaDto articulo)
    {
        // Si el artículo ya está en el ticket, incrementamos la cantidad
        var existente = Items.FirstOrDefault(i => i.IdArticulo == articulo.IdArticulo);
        if (existente != null)
        {
            existente.Cantidad++;
            RecalcularTotales();
            ItemSeleccionado = existente;
            return;
        }

        var nuevoItem = new ItemVentaPosViewModel
        {
            NumeroItem = Items.Count + 1,
            IdArticulo = articulo.IdArticulo,
            CodigoBarras = articulo.CodigoBarras,
            Descripcion = articulo.Descripcion,
            PrecioUnitario = articulo.PrecioVenta,
            Cantidad = 1,
            StockActual = articulo.StockActual,
            EsServicio = articulo.EsServicio
        };

        Items.Add(nuevoItem);
        ItemSeleccionado = nuevoItem;
        RecalcularTotales();
    }

    public void LimpiarBuscador()
    {
        TextoBusqueda = string.Empty;
        ResultadosBusqueda.Clear();
        MostrarPopupBusqueda = false;
        IndiceResultadoSeleccionado = -1;
    }

    private void ReenumerarItems()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].NumeroItem = i + 1;
        }
    }

    private void RecalcularTotales()
    {
        OnPropertyChanged(nameof(CantidadTotalArticulos));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(SubtotalFormateado));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalFormateado));
        OnPropertyChanged(nameof(PuedeCobrar));
    }
}
