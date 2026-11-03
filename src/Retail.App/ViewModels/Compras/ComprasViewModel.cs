using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Compras;

/// <summary>
/// ViewModel de la pantalla de ingreso de compras a distribuidores (Módulo 4.2): carga una factura o remito, ya sea
/// recibiendo un pedido emitido (Track 4.3, P2.4) o agregando artículos a mano con el buscador.
/// Todo acceso a datos pasa por <see cref="CargaSerializada"/> (H-19, Ley 4).
/// Las excepciones de dominio, de concurrencia y de unicidad se muestran como mensajes; solo los errores
/// inesperados van al log.
/// </summary>
public partial class ComprasViewModel : ObservableObject, IDisposable
{
    // Espera previa a consultar el catálogo mientras el operador tipea (mismo criterio que el listado de artículos).
    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    // C-5 (H-19): Serializa todos los accesos a datos de esta pantalla sobre el mismo DbContext y
    // cancela las cargas en curso al salir de ella.
    private readonly CargaSerializada _carga;

    private readonly IProveedorService _proveedorService;
    private readonly ICompraService _compraService;
    private readonly IPedidoService _pedidoService;
    private readonly ICompraDialogService _dialogService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ILogger<ComprasViewModel> _logger;

    // Renglones cuyo PropertyChanged escuchamos para recalcular el total y la habilitación del registro.
    private readonly HashSet<DetalleCompraItemViewModel> _renglonesObservados = new();

    // Verdadero mientras se revierte un cambio de proveedor rechazado: evita volver a pedir confirmación.
    private bool _revirtiendoProveedor;

    /// <summary>Opción del combo que significa "ningún proveedor elegido": no filtra pedidos ni habilita el buscador.</summary>
    private static readonly ProveedorDto TodosLosProveedores = new()
    {
        IdProveedor = 0,
        RazonSocial = "Todos los proveedores",
        Cuit = string.Empty
    };

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();

    /// <summary>Lo que lista el combo: "Todos los proveedores" y, a continuación, los proveedores reales.</summary>
    public ObservableCollection<ProveedorDto> OpcionesProveedor { get; } = new();

    public ObservableCollection<DetalleCompraItemViewModel> DetalleCompraItems { get; } = new();
    public ObservableCollection<ArticuloParaCompraDto> ResultadosBusqueda { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportarPedidoCommand))]
    [NotifyPropertyChangedFor(nameof(PuedeImportarPedido))]
    [NotifyPropertyChangedFor(nameof(TieneProveedor))]
    [NotifyPropertyChangedFor(nameof(OpcionProveedor))]
    private ProveedorDto? _proveedorSeleccionado;

    /// <summary>
    /// Selección del combo: nunca es <c>null</c>, porque "Todos los proveedores" es una opción más (equivale a ningún
    /// proveedor, <c>null</c> en <see cref="ProveedorSeleccionado"/>). El combo se enlaza a esta propiedad para no
    /// necesitar un conversor de null.
    /// </summary>
    public ProveedorDto OpcionProveedor
    {
        get => ProveedorSeleccionado ?? TodosLosProveedores;
        set => ProveedorSeleccionado = value.IdProveedor == TodosLosProveedores.IdProveedor ? null : value;
    }

    [ObservableProperty]
    private string _numeroComprobante = string.Empty;

    [ObservableProperty]
    private string _tipoComprobante = Compra.TipoFactura;

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
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstado))]
    private string? _mensajeEstado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    [ObservableProperty]
    private DetalleCompraItemViewModel? _renglonSeleccionado;

    // Buscador de artículos para agregar renglones a mano.
    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private ArticuloParaCompraDto? _resultadoSeleccionado;

    [ObservableProperty]
    private bool _mostrarResultados;

    [ObservableProperty]
    private bool _sinResultados;

    /// <summary>
    /// Indica si se puede invocar la acción de recibir un pedido. Con proveedor elegido lista solo sus pedidos; sin
    /// proveedor lista los de todos y la compra toma el del pedido que se elija.
    /// </summary>
    public bool PuedeImportarPedido => ProveedorSeleccionado is null || ProveedorSeleccionado.IdProveedor > 0;

    public bool TieneProveedor => ProveedorSeleccionado is not null;

    public bool TieneMensajeError => !string.IsNullOrEmpty(MensajeError);

    public bool TieneMensajeEstado => !string.IsNullOrEmpty(MensajeEstado);

    /// <summary>
    /// Total general de la factura de compra acumulando los subtotales de cada renglón.
    /// </summary>
    public decimal TotalCompra => DetalleCompraItems.Sum(i => i.Subtotal);

    /// <summary>Texto "Falta: …" con lo que impide registrar la compra; vacío si está completa.</summary>
    public string Faltantes
    {
        get
        {
            var faltantes = ObtenerFaltantes();
            return faltantes.Count == 0 ? string.Empty : "Falta: " + string.Join(", ", faltantes);
        }
    }

    public bool TieneFaltantes => ObtenerFaltantes().Count > 0;

    /// <summary>La compra está completa y no hay otra operación en curso: es el CanExecute del registro (F12).</summary>
    public bool PuedeRegistrar => !IsBusy && !TieneFaltantes;

    /// <summary>La grilla no tiene renglones: la vista muestra <see cref="TextoGrillaVacia"/> en su lugar.</summary>
    public bool GrillaVacia => DetalleCompraItems.Count == 0;

    public string TextoGrillaVacia => ProveedorSeleccionado is null
        ? "Reciba un pedido emitido o elija un proveedor para buscar sus artículos."
        : "Reciba un pedido emitido o busque un artículo de este proveedor para agregarlo.";

    /// <summary>
    /// Lista de comprobación de lo necesario para registrar la compra. Las cantidades y los costos inválidos solo
    /// aparecen mientras no se cumplen.
    /// </summary>
    public IReadOnlyList<RequisitoCompra> Requisitos
    {
        get
        {
            var requisitos = new List<RequisitoCompra>
            {
                new("Proveedor", ProveedorSeleccionado is not null),
                new("Número de comprobante", !string.IsNullOrWhiteSpace(NumeroComprobante)),
                new("Artículos", DetalleCompraItems.Count > 0)
            };

            if (DetalleCompraItems.Any(i => i.Cantidad < 1))
            {
                requisitos.Add(new RequisitoCompra("Cantidades mayores a 0", false));
            }

            if (DetalleCompraItems.Any(i => i.CostoUnitario <= 0m))
            {
                requisitos.Add(new RequisitoCompra("Costos mayores a 0", false));
            }

            return requisitos;
        }
    }

    /// <summary>Requisitos ya cumplidos, para mostrarlos tildados en el pie.</summary>
    public IReadOnlyList<string> RequisitosCumplidos => Requisitos.Where(r => r.Cumplido).Select(r => r.Texto).ToList();

    /// <summary>"Faltan: A y B" con lo que todavía impide registrar la compra; vacío si está completa.</summary>
    public string TextoFaltantes
    {
        get
        {
            var pendientes = Requisitos.Where(r => !r.Cumplido).Select(r => r.Texto).ToList();
            return pendientes.Count switch
            {
                0 => string.Empty,
                1 => $"Falta: {pendientes[0]}",
                _ => $"Faltan: {string.Join(", ", pendientes.Take(pendientes.Count - 1))} y {pendientes[^1]}"
            };
        }
    }

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

        DetalleCompraItems.CollectionChanged += OnRenglonesChanged;
    }

    partial void OnProveedorSeleccionadoChanged(ProveedorDto? oldValue, ProveedorDto? newValue)
    {
        if (!_revirtiendoProveedor && oldValue is not null && (DetalleCompraItems.Count > 0 || TienePedidoOrigen))
        {
            var cambiar = _dialogService.Confirmar(
                "Cambiar de proveedor",
                "La compra ya tiene artículos o un pedido vinculado del proveedor anterior.\n" +
                "Si cambia de proveedor se quitarán. ¿Desea continuar?");

            if (!cambiar)
            {
                // Se devuelve la selección al proveedor anterior; la marca evita volver a preguntar en la reentrada.
                _revirtiendoProveedor = true;
                try
                {
                    ProveedorSeleccionado = oldValue;
                }
                finally
                {
                    _revirtiendoProveedor = false;
                }

                return;
            }

            QuitarRenglonesYPedido();
        }

        // Lo que se había buscado era del proveedor anterior: el desplegable no debe seguir ofreciéndolo.
        TextoBusqueda = string.Empty;
        MensajeError = null;
        OnPropertyChanged(nameof(TextoGrillaVacia));
        ActualizarEstadoDerivado();
    }

    partial void OnNumeroComprobanteChanged(string value)
    {
        ActualizarEstadoDerivado();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ActualizarEstadoDerivado();
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        _ = BuscarConEsperaAsync(EsperaBusqueda);
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
                OpcionesProveedor.Clear();
                OpcionesProveedor.Add(TodosLosProveedores);
                if (lista != null)
                {
                    foreach (var p in lista)
                    {
                        Proveedores.Add(p);
                        OpcionesProveedor.Add(p);
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

    // ── Buscador de artículos ────────────────────────────────────────────────

    /// <summary>Consulta el catálogo sin espera previa (la usan las pruebas y el reintento manual).</summary>
    [RelayCommand]
    private Task BuscarArticulosAsync(CancellationToken cancellationToken = default)
    {
        return BuscarConEsperaAsync(TimeSpan.Zero, cancellationToken);
    }

    private async Task BuscarConEsperaAsync(TimeSpan espera, CancellationToken cancellationToken = default)
    {
        var termino = TextoBusqueda.Trim();
        var hayQueBuscar = termino.Length > 0 && TieneProveedor;

        // Solo se ofrecen los artículos del proveedor elegido (la restricción se resuelve en la consulta).
        var idProveedor = ProveedorSeleccionado?.IdProveedor ?? 0;

        if (!hayQueBuscar)
        {
            ResultadosBusqueda.Clear();
            ResultadoSeleccionado = null;
            MostrarResultados = false;
            SinResultados = false;
        }

        // Aun sin término se pasa por la carga reemplazable: así se descarta una búsqueda anterior todavía pendiente
        // y no reabre el desplegable cuando el operador ya borró el texto.
        await _carga.EjecutarAsync(
            token => hayQueBuscar
                ? Task.Run(() => _compraService.BuscarArticulosAsync(termino, idProveedor, cancellationToken: token), token)
                : Task.FromResult<IReadOnlyList<ArticuloParaCompraDto>>([]),
            resultados =>
            {
                if (!hayQueBuscar)
                {
                    return;
                }

                ResultadosBusqueda.Clear();
                foreach (var articulo in resultados)
                {
                    ResultadosBusqueda.Add(articulo);
                }

                ResultadoSeleccionado = ResultadosBusqueda.FirstOrDefault();
                SinResultados = ResultadosBusqueda.Count == 0;
                MostrarResultados = true;
            },
            ex =>
            {
                _logger.LogError(ex, "Error al buscar artículos para la compra");
                MensajeError = "No se pudo buscar en el catálogo de artículos.";
            },
            hayQueBuscar ? espera : TimeSpan.Zero,
            cancellationToken);
    }

    [RelayCommand]
    private void AgregarArticulo(ArticuloParaCompraDto? articulo)
    {
        if (articulo is null)
        {
            return;
        }

        MensajeError = null;

        var existente = DetalleCompraItems.FirstOrDefault(i => i.IdArticulo == articulo.IdArticulo);
        if (existente is not null)
        {
            // No se duplica el renglón: se lo marca para que el operador ajuste su cantidad.
            RenglonSeleccionado = existente;
            MensajeEstado = $"\"{articulo.Descripcion}\" ya está en la compra: ajuste su cantidad en la grilla.";
        }
        else
        {
            var renglon = DetalleCompraItemViewModel.DesdeArticulo(articulo);
            DetalleCompraItems.Add(renglon);
            RenglonSeleccionado = renglon;
            MensajeEstado = null;
        }

        TextoBusqueda = string.Empty;
    }

    [RelayCommand]
    private void AgregarResultadoSeleccionado()
    {
        AgregarArticulo(ResultadoSeleccionado);
    }

    [RelayCommand]
    private void MoverSeleccionBusquedaAbajo()
    {
        MoverSeleccionBusqueda(1);
    }

    [RelayCommand]
    private void MoverSeleccionBusquedaArriba()
    {
        MoverSeleccionBusqueda(-1);
    }

    /// <summary>Esc: cierra el desplegable; si ya estaba cerrado, borra el texto.</summary>
    [RelayCommand]
    private void CerrarBusqueda()
    {
        if (MostrarResultados)
        {
            MostrarResultados = false;
        }
        else
        {
            TextoBusqueda = string.Empty;
        }
    }

    private void MoverSeleccionBusqueda(int paso)
    {
        if (ResultadosBusqueda.Count == 0)
        {
            return;
        }

        var indice = ResultadoSeleccionado is null ? -1 : ResultadosBusqueda.IndexOf(ResultadoSeleccionado);
        var nuevo = Math.Clamp(indice + paso, 0, ResultadosBusqueda.Count - 1);
        ResultadoSeleccionado = ResultadosBusqueda[nuevo];
    }

    // ── Renglones ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void QuitarRenglon(DetalleCompraItemViewModel? renglon)
    {
        if (renglon is null)
        {
            return;
        }

        if (ReferenceEquals(RenglonSeleccionado, renglon))
        {
            RenglonSeleccionado = null;
        }

        DetalleCompraItems.Remove(renglon);
    }

    private void OnRenglonesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                Desobservar(e.OldItems);
                Observar(e.NewItems);
                break;

            default:
                // Reset (Clear) no informa qué renglones salieron: se vuelve a sincronizar todo.
                foreach (var renglon in _renglonesObservados)
                {
                    renglon.PropertyChanged -= OnRenglonPropertyChanged;
                }

                _renglonesObservados.Clear();
                Observar(DetalleCompraItems);
                break;
        }

        OnPropertyChanged(nameof(GrillaVacia));
        ActualizarEstadoDerivado();
    }

    private void Observar(IList? renglones)
    {
        if (renglones is null)
        {
            return;
        }

        foreach (DetalleCompraItemViewModel renglon in renglones)
        {
            if (_renglonesObservados.Add(renglon))
            {
                renglon.PropertyChanged += OnRenglonPropertyChanged;
            }
        }
    }

    private void Desobservar(IList? renglones)
    {
        if (renglones is null)
        {
            return;
        }

        foreach (DetalleCompraItemViewModel renglon in renglones)
        {
            if (_renglonesObservados.Remove(renglon))
            {
                renglon.PropertyChanged -= OnRenglonPropertyChanged;
            }
        }
    }

    private void OnRenglonPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ActualizarEstadoDerivado();
    }

    /// <summary>Notifica lo que depende de los renglones y del encabezado: total, faltantes y habilitación del registro.</summary>
    private void ActualizarEstadoDerivado()
    {
        OnPropertyChanged(nameof(TotalCompra));
        OnPropertyChanged(nameof(Requisitos));
        OnPropertyChanged(nameof(RequisitosCumplidos));
        OnPropertyChanged(nameof(TextoFaltantes));
        OnPropertyChanged(nameof(Faltantes));
        OnPropertyChanged(nameof(TieneFaltantes));
        OnPropertyChanged(nameof(PuedeRegistrar));
        RegistrarCompraCommand.NotifyCanExecuteChanged();
    }

    private List<string> ObtenerFaltantes()
    {
        var faltantes = new List<string>();

        if (ProveedorSeleccionado is null)
        {
            faltantes.Add("proveedor");
        }

        if (string.IsNullOrWhiteSpace(NumeroComprobante))
        {
            faltantes.Add("número de comprobante");
        }

        if (DetalleCompraItems.Count == 0)
        {
            faltantes.Add("al menos un artículo");
        }
        else
        {
            if (DetalleCompraItems.Any(i => i.Cantidad < 1))
            {
                faltantes.Add("cantidad recibida mayor a 0 en todos los artículos");
            }

            if (DetalleCompraItems.Any(i => i.CostoUnitario <= 0m))
            {
                faltantes.Add("costo mayor a 0 en todos los artículos");
            }
        }

        return faltantes;
    }

    // ── Pedido emitido ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(PuedeImportarPedido))]
    private async Task ImportarPedidoAsync()
    {
        if (DetalleCompraItems.Count > 0)
        {
            var confirmar = _dialogService.Confirmar(
                "Recibir pedido",
                "La compra ya contiene artículos cargados.\n¿Desea reemplazarlos con los del pedido seleccionado?");

            if (!confirmar)
            {
                return;
            }
        }

        var pedidoElegido = await _dialogService.MostrarSeleccionarPedidoModalAsync(
            ProveedorSeleccionado?.IdProveedor,
            ProveedorSeleccionado?.RazonSocial);
        if (pedidoElegido is null)
        {
            return;
        }

        IsBusy = true;
        MensajeError = null;
        MensajeEstado = null;
        PedidoParaCompraDto? pedidoParaCompra = null;
        var huboError = false;

        try
        {
            // Operación no reemplazable: escribir en el buscador mientras se lee el pedido no debe cancelarla.
            await _carga.EjecutarOperacionAsync(
                token => Task.Run(() => _pedidoService.ObtenerParaCompraAsync(pedidoElegido.IdPedido, token), token),
                resultado => pedidoParaCompra = resultado,
                ex =>
                {
                    huboError = true;
                    _logger.LogError(ex, "Error al obtener el pedido {IdPedido} para compra", pedidoElegido.IdPedido);
                    MensajeError = "Error al obtener los detalles del pedido seleccionado.";
                });
        }
        finally
        {
            IsBusy = false;
        }

        if (huboError)
        {
            // Falló la lectura: no es lo mismo que un pedido que dejó de estar disponible, así que no hay alerta.
            return;
        }

        if (pedidoParaCompra is null)
        {
            _dialogService.MostrarAlerta(
                "Pedido no disponible",
                "El pedido seleccionado ya no está disponible (fue cancelado o recibido en otra terminal).");
            MensajeError = "El pedido seleccionado ya no está disponible.";
            return;
        }

        // Sin proveedor previo la compra toma el del pedido; con proveedor previo el diálogo ya filtró por él.
        // Con el proveedor anterior nulo, el cambio de selección no pide confirmación.
        if (ProveedorSeleccionado is null)
        {
            var proveedorDelPedido = Proveedores.FirstOrDefault(p => p.IdProveedor == pedidoParaCompra.IdProveedor);
            if (proveedorDelPedido is null)
            {
                MensajeError = "No se encontró el proveedor del pedido seleccionado. Recargue la pantalla e intente de nuevo.";
                return;
            }

            ProveedorSeleccionado = proveedorDelPedido;
        }

        VaciarRenglones();
        foreach (var renglon in pedidoParaCompra.Renglones)
        {
            DetalleCompraItems.Add(DetalleCompraItemViewModel.DesdeRenglonDePedido(renglon));
        }

        IdPedidoOrigen = pedidoParaCompra.IdPedido;
        InformacionPedidoOrigen = $"Pedido #{pedidoParaCompra.IdPedido}";
        TienePedidoOrigen = true;
    }

    /// <summary>
    /// Quita el pedido de la compra y, con él, los artículos que traía: una grilla no puede conservar renglones de un
    /// origen que ya no está vinculado.
    /// </summary>
    [RelayCommand]
    private void DesvincularPedido()
    {
        QuitarRenglonesYPedido();
    }

    private void DesvincularPedidoOrigen()
    {
        IdPedidoOrigen = null;
        InformacionPedidoOrigen = null;
        TienePedidoOrigen = false;
    }

    // ── Registro ─────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(PuedeRegistrar))]
    private async Task RegistrarCompraAsync()
    {
        // El comando ya queda deshabilitado con faltantes; esta guarda cubre las invocaciones directas.
        if (ProveedorSeleccionado is null || DetalleCompraItems.Count == 0)
        {
            MensajeError = Faltantes;
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

                        case RestriccionDeUnicidadException:
                            // El índice único (proveedor, tipo, número) rechazó un comprobante que otra terminal
                            // registró entre la validación y el guardado.
                            MensajeError = "Ese comprobante ya fue registrado para este proveedor (posiblemente desde otra terminal).";
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

    private void VaciarRenglones()
    {
        RenglonSeleccionado = null;
        DetalleCompraItems.Clear();
    }

    private void QuitarRenglonesYPedido()
    {
        VaciarRenglones();
        DesvincularPedidoOrigen();
    }

    private void LimpiarFormulario()
    {
        // Primero los renglones y el pedido: así cambiar el proveedor a nulo no pide confirmación.
        QuitarRenglonesYPedido();
        NumeroComprobante = string.Empty;
        TipoComprobante = Compra.TipoFactura;
        FechaEmision = DateTime.Today;
        TextoBusqueda = string.Empty;
        ProveedorSeleccionado = null;
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
