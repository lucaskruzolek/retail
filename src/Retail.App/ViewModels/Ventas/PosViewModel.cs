using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Caja;
using Retail.Application.DTOs.Clientes;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// ViewModel principal para el Punto de Venta (POS) y terminal de mostrador (RF-09, RF-10, RNF-01).
/// Orquesta el SearchBar unificado con popup predictivo, la grilla del ticket contable,
/// atajos F1 a F12 y el despacho hacia el checkout multimedio.
/// </summary>
public partial class PosViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo CulturaArgentina = CultureInfo.GetCultureInfo("es-AR");

    private readonly IVentaService _ventaService;
    private readonly ICajaService _cajaService;
    private readonly IInventarioService _inventarioService;
    private readonly ICurrentUserSession _session;
    private readonly IVentaDialogService _dialogService;
    private readonly ILogger<PosViewModel> _logger;

    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(180);

    // Todos los accesos a datos del mostrador (búsqueda predictiva, artículo escaneado y estado de la caja)
    // pasan por esta fila: nunca usan el DbContext de la pantalla al mismo tiempo y se cancelan al salir (H-19).
    private readonly CargaSerializada _accesoDatos = new();

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
        IInventarioService inventarioService,
        ICurrentUserSession session,
        IVentaDialogService dialogService,
        ILogger<PosViewModel>? logger = null)
    {
        _ventaService = ventaService ?? throw new ArgumentNullException(nameof(ventaService));
        _cajaService = cajaService ?? throw new ArgumentNullException(nameof(cajaService));
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
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
        string texto = value.Trim();

        if (texto.Length < 2)
        {
            // Una carga vacía reemplaza a la búsqueda en curso, cuyos resultados ya no corresponden al texto.
            _ = _accesoDatos.EjecutarAsync(
                _ => Task.FromResult<IReadOnlyList<ArticuloVentaDto>>(Array.Empty<ArticuloVentaDto>()),
                MostrarResultadosBusqueda,
                _ => { });
            OcultarResultadosBusqueda();
            return;
        }

        _ = _accesoDatos.EjecutarAsync(
            token => Task.Run(() => BuscarSugerenciasAsync(texto, token), token),
            MostrarResultadosBusqueda,
            ex => _logger.LogError(ex, "Error en la búsqueda predictiva de artículos para mostrador."),
            EsperaBusqueda);
    }

    private async Task<IReadOnlyList<ArticuloVentaDto>> BuscarSugerenciasAsync(string texto, CancellationToken token)
    {
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

        return lista;
    }

    private void MostrarResultadosBusqueda(IReadOnlyList<ArticuloVentaDto> lista)
    {
        ResultadosBusqueda.Clear();
        foreach (var item in lista)
        {
            ResultadosBusqueda.Add(item);
        }

        MostrarPopupBusqueda = ResultadosBusqueda.Count > 0;
        IndiceResultadoSeleccionado = ResultadosBusqueda.Count > 0 ? 0 : -1;
    }

    private void OcultarResultadosBusqueda()
    {
        ResultadosBusqueda.Clear();
        MostrarPopupBusqueda = false;
        IndiceResultadoSeleccionado = -1;
    }

    [RelayCommand]
    public async Task ProcesarEnterAsync()
    {
        // 1. Si hay un ítem resaltado en el popup desplegado, cargarlo
        if (MostrarPopupBusqueda && IndiceResultadoSeleccionado >= 0 && IndiceResultadoSeleccionado < ResultadosBusqueda.Count)
        {
            var seleccionado = ResultadosBusqueda[IndiceResultadoSeleccionado];
            AgregarArticuloAlTicket(seleccionado);
            LimpiarBuscador();
            return;
        }

        // 2. Si se ingresó un valor directo en el campo sin navegar el popup (típico del lector de códigos)
        string entrada = TextoBusqueda.Trim();
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return;
        }

        // Limpiar el buscador reemplaza la búsqueda predictiva en curso; la búsqueda del artículo espera
        // su turno para no usar el DbContext al mismo tiempo que ella (H-19).
        LimpiarBuscador();

        await _accesoDatos.EjecutarOperacionAsync(
            token => Task.Run(() => _ventaService.BuscarArticuloParaVentaAsync(entrada, token), token),
            articulo =>
            {
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
            },
            ex =>
            {
                _logger.LogError(ex, "Error al buscar el artículo ingresado en mostrador: {Entrada}", entrada);
                _dialogService.MostrarError("Error de búsqueda", ex.Message);
            });
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
            VaciarTicket();
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

        // Sin turno o sin usuario la venta no se puede imputar: no se inventa un valor por defecto.
        if (TurnoActivo is not { } turno || _session.IdUsuario is not int idUsuario)
        {
            _dialogService.MostrarAlerta(
                "Sesión incompleta",
                "No se pudo identificar el turno de caja o el usuario de la sesión. Vuelva a cargar el estado de la caja o a iniciar sesión.");
            return;
        }

        try
        {
            IsBusy = true;

            if (!await PrepararTicketParaCobroAsync())
            {
                return;
            }

            IsBusy = false;
            var pagos = await _dialogService.MostrarCobroModalAsync(Total, Cliente);
            if (pagos == null || pagos.Count == 0)
            {
                // Operación cancelada por el cajero
                return;
            }

            IsBusy = true;
            var crearVentaDto = new CrearVentaDto
            {
                IdTurno = turno.IdTurno,
                IdUsuario = idUsuario,
                IdCliente = Cliente?.IdCliente,
                Descuento = Descuento,
                Items = CrearDetallesDelTicket(),
                Pagos = pagos
            };

            var (completado, _) = await EjecutarEnFilaAsync(token => _ventaService.RegistrarVentaAsync(crearVentaDto, token));
            if (completado)
            {
                VaciarTicket();
            }
        }
        catch (ValidationException ex)
        {
            _dialogService.MostrarAlerta("Venta no registrada", string.Join("\n", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            // Otra terminal vendió o modificó los mismos artículos: el ticket se conserva para reintentar.
            _dialogService.MostrarAlerta("Datos actualizados por otra terminal", ex.Message);
        }
        catch (DomainException ex)
        {
            // Regla de negocio (stock, límite de crédito, precio cambiado): es información para el cajero, no una
            // falla del sistema. El ticket se conserva.
            _dialogService.MostrarAlerta("Venta no registrada", ex.Message);
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

    /// <summary>
    /// Verifica el ticket contra el catálogo antes de abrir el cobro: actualiza los precios que cambiaron (D-14),
    /// frena si hay artículos dados de baja o sin stock, y ofrece fraccionar las presentaciones (RF-10, D-13).
    /// Es una ayuda para el cajero; RegistrarVentaAsync vuelve a validar todo al confirmar.
    /// </summary>
    /// <returns><c>false</c> si el cobro no puede continuar.</returns>
    private async Task<bool> PrepararTicketParaCobroAsync()
    {
        var detalles = CrearDetallesDelTicket();
        var (completado, verificacion) = await EjecutarEnFilaAsync(token => _ventaService.VerificarTicketAsync(detalles, token));
        if (!completado)
        {
            return false;
        }

        if (verificacion.ArticulosNoDisponibles.Count > 0)
        {
            _dialogService.MostrarAlerta(
                "Artículos dados de baja",
                "Quite del ticket los siguientes artículos, que ya no están disponibles:\n" +
                string.Join("\n", verificacion.ArticulosNoDisponibles.Select(d => $"• {d}")));
            return false;
        }

        if (verificacion.PreciosActualizados.Count > 0)
        {
            AplicarPreciosActualizados(verificacion.PreciosActualizados);
        }

        // Si algún faltante no se puede cubrir, se informan todos y no se fracciona nada: abrir un pack para una venta
        // que igual no se va a poder cobrar dejaría stock movido sin motivo.
        var sinSolucion = verificacion.Faltantes.Where(f => !f.PuedeFraccionar).ToList();
        if (sinSolucion.Count > 0)
        {
            _dialogService.MostrarAlerta(
                "Stock insuficiente",
                string.Join("\n", sinSolucion.Select(DescribirFaltante)));
            return false;
        }

        foreach (var faltante in verificacion.Faltantes)
        {
            if (!await OfrecerFraccionarAsync(faltante))
            {
                return false;
            }
        }

        return true;
    }

    private void AplicarPreciosActualizados(IReadOnlyList<PrecioActualizadoDto> preciosActualizados)
    {
        foreach (var cambio in preciosActualizados)
        {
            var item = Items.FirstOrDefault(i => i.IdArticulo == cambio.IdArticulo);
            if (item != null)
            {
                item.PrecioUnitario = cambio.PrecioActual;
            }
        }

        RecalcularTotales();

        _dialogService.MostrarAlerta(
            "Precios actualizados",
            "Los siguientes precios cambiaron en el catálogo y se actualizaron en el ticket:\n" +
            string.Join("\n", preciosActualizados.Select(c =>
                $"• {c.Descripcion}: {c.PrecioAnterior.ToString("C2", CulturaArgentina)} → {c.PrecioActual.ToString("C2", CulturaArgentina)}")) +
            $"\n\nNuevo total: {TotalFormateado}");
    }

    /// <summary>
    /// RF-10: el POS ofrece fraccionar el origen, pero no lo hace sin la confirmación del cajero.
    /// </summary>
    private async Task<bool> OfrecerFraccionarAsync(FaltanteStockDto faltante)
    {
        int unidadesQueSeSuman = faltante.OrigenesAFraccionar * (faltante.UnidadesPorOrigen ?? 0);
        bool confirma = _dialogService.Confirmar(
            "Fraccionar presentación",
            $"Faltan {faltante.Faltante} de '{faltante.Descripcion}' (hay {faltante.StockActual}).\n\n" +
            $"¿Abrir {faltante.OrigenesAFraccionar} de '{faltante.DescripcionOrigen}' (quedan {faltante.StockOrigen})? " +
            $"Se sumarán {unidadesQueSeSuman} unidades.");

        if (!confirma)
        {
            return false;
        }

        var fraccionar = new FraccionarDto
        {
            IdArticuloDerivado = faltante.IdArticulo,
            CantidadOrigen = faltante.OrigenesAFraccionar
        };
        var (completado, unidadesObtenidas) = await EjecutarEnFilaAsync(token => _inventarioService.FraccionarAsync(fraccionar, token));
        if (!completado)
        {
            return false;
        }

        var item = Items.FirstOrDefault(i => i.IdArticulo == faltante.IdArticulo);
        if (item != null)
        {
            item.StockActual = faltante.StockActual + unidadesObtenidas;
        }

        return true;
    }

    private static string DescribirFaltante(FaltanteStockDto faltante)
    {
        string detalle = $"• {faltante.Descripcion}: se piden {faltante.CantidadSolicitada} y hay {faltante.StockActual}.";

        if (faltante.OrigenesAFraccionar > 0 && faltante.DescripcionOrigen != null)
        {
            detalle += $" Tampoco alcanza fraccionando: harían falta {faltante.OrigenesAFraccionar} de '{faltante.DescripcionOrigen}' y quedan {faltante.StockOrigen}.";
        }

        return detalle;
    }

    private List<DetalleVentaDto> CrearDetallesDelTicket()
    {
        return Items.Select(i => new DetalleVentaDto
        {
            IdArticulo = i.IdArticulo,
            CodigoBarras = i.CodigoBarras,
            Descripcion = i.Descripcion,
            Cantidad = i.Cantidad,
            PrecioUnitario = i.PrecioUnitario
        }).ToList();
    }

    private void VaciarTicket()
    {
        Items.Clear();
        Descuento = 0m;
        Cliente = null;
        RecalcularTotales();
    }

    /// <summary>
    /// Ejecuta un acceso a datos del cobro en la fila de la pantalla, para no usar el DbContext al mismo tiempo que
    /// una búsqueda (H-19), y fuera del hilo de la UI (Ley 4). Convierte los callbacks de <see cref="CargaSerializada"/>
    /// en un flujo secuencial: relanza el error para que lo trate quien llama.
    /// </summary>
    /// <returns><c>Completado = false</c> si la pantalla se descartó y el acceso se canceló.</returns>
    private async Task<(bool Completado, T Resultado)> EjecutarEnFilaAsync<T>(Func<CancellationToken, Task<T>> acceso)
    {
        bool completado = false;
        T resultado = default!;
        ExceptionDispatchInfo? error = null;

        await _accesoDatos.EjecutarOperacionAsync(
            token => Task.Run(() => acceso(token), token),
            valor =>
            {
                resultado = valor;
                completado = true;
            },
            ex => error = ExceptionDispatchInfo.Capture(ex));

        error?.Throw();
        return (completado, resultado);
    }

    [RelayCommand]
    public Task CargarEstadoCajaAsync()
    {
        return _accesoDatos.EjecutarOperacionAsync(
            token => Task.Run(() => _cajaService.ObtenerTurnoActivoAsync(token), token),
            turno =>
            {
                TurnoActivo = turno;
                CajaAbierta = TurnoActivo?.Estado == EstadoTurnoEnum.Abierto;
            },
            ex =>
            {
                _logger.LogWarning(ex, "No se pudo recuperar el turno de caja activo: {Mensaje}", ex.Message);
                CajaAbierta = false;
            });
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

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela los accesos a datos pendientes para que no
    /// usen el DbContext ya descartado ni muestren errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _accesoDatos.Dispose();
        GC.SuppressFinalize(this);
    }
}
