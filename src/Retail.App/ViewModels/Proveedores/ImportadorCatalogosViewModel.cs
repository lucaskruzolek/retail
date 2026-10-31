using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Pedidos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para la consulta y curaduría del catálogo de distribuidores e incorporación asistida a tienda (Etapa 2.2).
/// Orquesta la vista previa del catálogo con push-down a SQL, filtros reactivos y la incorporación controlada a tienda.
/// </summary>
public partial class ImportadorCatalogosViewModel : ObservableObject, IDisposable
{
    private readonly IProveedorService _proveedorService;
    private readonly IProveedorDialogService _dialogService;
    private readonly IInventarioService _inventarioService;
    private readonly INavigationService _navigationService;
    private readonly FlujoPedirArticulos _flujoPedir;

    // Se resuelve por DI (con los servicios de clasificación de esta pantalla) y se reinicia en cada apertura del modal.
    private readonly IncorporarArticulosModalViewModel _incorporarModalViewModel;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ImportadorCatalogosViewModel> _logger;

    /// <summary>
    /// Antigüedad a partir de la cual la lista del proveedor se marca como desactualizada: una lista vieja y barata
    /// es la trampa que motivó A-2 del Track 4.3.
    /// </summary>
    public const int DiasListaDesactualizada = 30;

    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    // Serializa las cargas del catálogo sobre el DbContext de esta pantalla y las cancela al salir de ella (H-18, H-19).
    private readonly CargaSerializada _cargaCatalogo;

    // ------------------------------------------------------------------ //
    // Proveedor activo y selección                                       //
    // ------------------------------------------------------------------ //

    public ObservableCollection<ProveedorDto> ProveedoresDisponibles { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneProveedorActivo))]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private ProveedorDto? _proveedorActivo;

    public bool TieneProveedorActivo => ProveedorActivo != null;

    /// <summary>Fecha (hora local) de la última planilla importada del proveedor activo; nula si no tiene catálogo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoUltimaPlanilla))]
    [NotifyPropertyChangedFor(nameof(ListaDesactualizada))]
    private DateTime? _fechaUltimaPlanilla;

    /// <summary>Días transcurridos desde la última planilla, en días calendario locales.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoUltimaPlanilla))]
    [NotifyPropertyChangedFor(nameof(ListaDesactualizada))]
    private int? _diasDesdeUltimaPlanilla;

    public string TextoUltimaPlanilla => FechaUltimaPlanilla is { } fecha && DiasDesdeUltimaPlanilla is { } dias
        ? $"Última planilla: {fecha:dd/MM/yyyy} ({TextoAntiguedad(dias)})"
        : "Todavía no se importó ninguna planilla de este proveedor";

    public bool ListaDesactualizada => DiasDesdeUltimaPlanilla > DiasListaDesactualizada;

    public string AvisoListaDesactualizada { get; } =
        $"La lista tiene más de {DiasListaDesactualizada} días: sus costos pueden no estar vigentes. Importe una planilla nueva antes de pedir.";

    // ------------------------------------------------------------------ //
    // Estado del importador y progreso                                   //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeImportar))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstado))]
    private string? _mensajeEstado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeEstado => !string.IsNullOrWhiteSpace(MensajeEstado);
    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    [ObservableProperty]
    private ResultadoImportacionDto? _ultimoResultado;

    [ObservableProperty]
    private bool _tieneItemsSeleccionados;

    [ObservableProperty]
    private bool? _seleccionarTodos = false;

    private bool _isUpdatingSeleccionarTodos;

    public bool PuedeImportar => TieneProveedorActivo && !IsBusy;

    /// <summary>Solo el Gerente y el Encargado ven las acciones de pedir (A-14).</summary>
    public bool PuedePedir => _flujoPedir.PuedePedir;

    // ------------------------------------------------------------------ //
    // Catálogo importado, filtros y paginación                            //
    // ------------------------------------------------------------------ //

    public ObservableCollection<CatalogoProveedorDto> ItemsCatalogo { get; } = new();

    public IReadOnlyList<EstadoVinculacionCatalogoEnum> OpcionesEstado { get; } = Enum.GetValues<EstadoVinculacionCatalogoEnum>();

    [ObservableProperty]
    private string _textoBusquedaCatalogo = string.Empty;

    [ObservableProperty]
    private EstadoVinculacionCatalogoEnum _filtroEstado = EstadoVinculacionCatalogoEnum.Todos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _totalItemsCatalogo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _totalPaginas = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _tamanoPagina = 50;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = new[] { 20, 50, 100 };

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalItemsCatalogo == 0
        ? "Sin artículos"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalItemsCatalogo)} de {TotalItemsCatalogo} artículos";

    public ImportadorCatalogosViewModel(
        IProveedorService proveedorService,
        IProveedorDialogService dialogService,
        IInventarioService inventarioService,
        INavigationService navigationService,
        IncorporarArticulosModalViewModel incorporarModalViewModel,
        FlujoPedirArticulos flujoPedir,
        TimeProvider reloj,
        ILogger<ImportadorCatalogosViewModel> logger)
    {
        _incorporarModalViewModel = incorporarModalViewModel ?? throw new ArgumentNullException(nameof(incorporarModalViewModel));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _flujoPedir = flujoPedir ?? throw new ArgumentNullException(nameof(flujoPedir));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cargaCatalogo = new CargaSerializada(_logger);
    }

    public async Task CargarProveedoresDisponiblesAsync(int? idProveedorSeleccionar = null)
    {
        try
        {
            var proveedores = await Task.Run(() => _proveedorService.ListarProveedoresAsync());
            ProveedoresDisponibles.Clear();
            foreach (var p in proveedores)
            {
                ProveedoresDisponibles.Add(p);
            }

            if (idProveedorSeleccionar.HasValue)
            {
                ProveedorActivo = ProveedoresDisponibles.FirstOrDefault(p => p.IdProveedor == idProveedorSeleccionar.Value);
            }
            else if (ProveedorActivo == null && ProveedoresDisponibles.Count > 0)
            {
                ProveedorActivo = ProveedoresDisponibles[0];
            }
            else if (ProveedorActivo != null)
            {
                ProveedorActivo = ProveedoresDisponibles.FirstOrDefault(p => p.IdProveedor == ProveedorActivo.IdProveedor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar la lista de proveedores disponibles");
            MensajeError = "No se pudo cargar la lista de proveedores.";
        }
    }

    public async Task InicializarAsync(ProveedorDto? proveedor = null)
    {
        await CargarProveedoresDisponiblesAsync(proveedor?.IdProveedor);
        if (proveedor != null)
        {
            if (!ProveedoresDisponibles.Any(p => p.IdProveedor == proveedor.IdProveedor))
            {
                ProveedoresDisponibles.Add(proveedor);
            }
            ProveedorActivo = proveedor;
        }

        PaginaActual = 1;
        if (ProveedorActivo != null)
        {
            await CargarCatalogoAsync();
        }
    }

    public void Inicializar(ProveedorDto? proveedor = null)
    {
        _ = InicializarAsync(proveedor);
    }

    partial void OnProveedorActivoChanged(ProveedorDto? value)
    {
        PaginaActual = 1;
        LimpiarItemsCatalogo();
        TotalItemsCatalogo = 0;
        TotalPaginas = 1;
        MensajeError = null;
        MensajeEstado = null;
        UltimoResultado = null;
        TieneItemsSeleccionados = false;
        SeleccionarTodos = false;
        MostrarUltimaPlanilla(null);

        if (value != null)
        {
            _ = CargarCatalogoAsync();
        }
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        _ = CargarCatalogoAsync();
    }

    partial void OnFiltroEstadoChanged(EstadoVinculacionCatalogoEnum value)
    {
        PaginaActual = 1;
        _ = CargarCatalogoAsync();
    }

    partial void OnTextoBusquedaCatalogoChanged(string value)
    {
        PaginaActual = 1;
        _ = CargarCatalogoConEsperaAsync(EsperaBusqueda);
    }

    // ------------------------------------------------------------------ //
    // Comandos de Importación                                            //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task ImportarPlanillaAsync()
    {
        if (ProveedorActivo is null)
        {
            return;
        }

        var resultado = await _dialogService.AbrirImportarPlanillaAsync(ProveedorActivo);
        if (resultado != null)
        {
            UltimoResultado = resultado;
            MensajeEstado = $"Importación completada: {resultado.NuevosRegistros} nuevos, {resultado.PreciosActualizados} precios actualizados, {resultado.FilasConError} errores.";
            PaginaActual = 1;
            await CargarCatalogoAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Explorador de Catálogo y Paginación en Servidor                    //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    public Task CargarCatalogoAsync(CancellationToken cancellationToken = default)
    {
        return CargarCatalogoConEsperaAsync(TimeSpan.Zero, cancellationToken);
    }

    /// <summary>
    /// Carga una página del catálogo a través de <see cref="CargaSerializada"/>: cada carga nueva cancela la
    /// anterior y espera a que libere el DbContext de la pantalla, y solo la vigente actualiza la grilla (H-18, H-19).
    /// </summary>
    private async Task CargarCatalogoConEsperaAsync(TimeSpan espera, CancellationToken cancellationToken = default)
    {
        if (ProveedorActivo is null)
        {
            return;
        }

        var consulta = new ConsultaCatalogoProveedorDto
        {
            IdProveedor = ProveedorActivo.IdProveedor,
            TerminoBusqueda = TextoBusquedaCatalogo,
            EstadoVinculacion = FiltroEstado,
            Pagina = PaginaActual,
            TamañoPagina = TamanoPagina
        };

        IsBusy = true;
        MensajeError = null;

        await _cargaCatalogo.EjecutarAsync(
            token => Task.Run(() => _proveedorService.ListarItemsCatalogoAsync(consulta, token), token),
            MostrarCatalogo,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar catálogo del proveedor");
                MensajeError = "No se pudo cargar el catálogo.";
            },
            espera,
            cancellationToken);

        IsBusy = _cargaCatalogo.EnCurso;
    }

    private void MostrarCatalogo(CatalogoPaginadoDto resultado)
    {
        LimpiarItemsCatalogo();
        foreach (var item in resultado.Items)
        {
            item.PropertyChanged += Item_PropertyChanged;
            ItemsCatalogo.Add(item);
        }

        TotalItemsCatalogo = resultado.TotalRegistros;
        TotalPaginas = Math.Max(1, resultado.TotalPaginas);
        MostrarUltimaPlanilla(resultado.FechaUltimaPlanilla);

        _isUpdatingSeleccionarTodos = true;
        try
        {
            SeleccionarTodos = false;
            TieneItemsSeleccionados = false;
        }
        finally
        {
            _isUpdatingSeleccionarTodos = false;
        }

        OnPropertyChanged(nameof(PuedeRetrocederPagina));
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));
    }

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarCatalogoAsync();
        }
    }

    /// <summary>Va a una página puntual (los números de la barra de paginación); fuera de rango no hace nada.</summary>
    [RelayCommand]
    public async Task IrAPaginaAsync(int pagina)
    {
        if (pagina >= 1 && pagina <= TotalPaginas && pagina != PaginaActual)
        {
            PaginaActual = pagina;
            await CargarCatalogoAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Curaduría y Vinculación Asistida (RF-05)                           //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task VincularArticuloAsync(CatalogoProveedorDto? item)
    {
        if (item is null)
        {
            return;
        }

        var vinculado = await _dialogService.AbrirSelectorVinculacionArticuloAsync(item);
        if (vinculado)
        {
            MensajeEstado = $"Artículo vinculado con éxito al código {item.CodigoProveedor}.";
            await CargarCatalogoAsync();
        }
    }

    [RelayCommand]
    private async Task IncorporarSeleccionadosAsync(IList<CatalogoProveedorDto>? itemsSeleccionadosParam)
    {
        var itemsSeleccionados = (itemsSeleccionadosParam != null && itemsSeleccionadosParam.Count > 0)
            ? itemsSeleccionadosParam
            : ItemsCatalogo.Where(i => i.EstaSeleccionado).ToList();

        if (itemsSeleccionados is null || itemsSeleccionados.Count == 0 || ProveedorActivo is null)
        {
            MensajeError = "Seleccione al menos un artículo del catálogo para incorporar.";
            return;
        }

        MensajeError = null;
        var mensaje = await IncorporarAsync(itemsSeleccionados);
        if (mensaje is null)
        {
            return;
        }

        MensajeEstado = mensaje;
        TieneItemsSeleccionados = false;
        SeleccionarTodos = false;
        await CargarCatalogoAsync();
    }

    /// <summary>
    /// Abre el modal de incorporación con los ítems y, si el usuario confirma, los incorpora a la tienda (RF-05). Lo
    /// comparten "Incorporar seleccionados" y el primer paso de "Pedir" (G5). Devuelve el mensaje de lo incorporado, o
    /// <c>null</c> si el usuario canceló o falló (el error queda en <see cref="MensajeError"/>).
    /// </summary>
    /// <remarks>
    /// Los dos accesos a datos (los diccionarios antes del modal y la incorporación después) esperan su turno en la
    /// fila de la pantalla y corren fuera del Dispatcher: el DbContext es uno solo y no admite dos operaciones a la vez
    /// (Ley 4, H-19). Si se sale de la pantalla, la fila los descarta sin informar error.
    /// </remarks>
    private async Task<string?> IncorporarAsync(IList<CatalogoProveedorDto> items)
    {
        (IReadOnlyList<CategoriaDto> Categorias, IReadOnlyList<MarcaDto> Marcas)? diccionarios = null;
        Exception? error = null;
        await _cargaCatalogo.EjecutarOperacionAsync(
            token => Task.Run(
                async () =>
                {
                    // Secuenciales: comparten el DbContext.
                    var categorias = await _inventarioService.ListarCategoriasAsync(token);
                    var marcas = await _inventarioService.ListarMarcasAsync(token);
                    return (Categorias: categorias, Marcas: marcas);
                },
                token),
            d => diccionarios = d,
            ex => error = ex);

        if (diccionarios is not { } d)
        {
            InformarErrorDeIncorporacion(error);
            return null;
        }

        var modalVm = _incorporarModalViewModel;
        modalVm.Inicializar(d.Categorias, d.Marcas, items);

        if (!_dialogService.MostrarIncorporacion(modalVm))
        {
            return null;
        }

        var dto = modalVm.ObtenerDto();
        string? mensaje = null;
        IsBusy = true;
        try
        {
            await _cargaCatalogo.EjecutarOperacionAsync(
                async token =>
                {
                    var resultado = await Task.Run(() => _proveedorService.IncorporarArticulosATiendaAsync(dto, token), token);

                    // Aviso no bloqueante de reglas nuevas para revisar (Track 6.6, paso 6.8): un fallo no afecta lo
                    // ya incorporado. Va en el mismo turno porque también usa el DbContext de la pantalla.
                    string? avisoReglas = await modalVm.ObtenerAvisoReglasPropuestasAsync();
                    return (Resultado: resultado, AvisoReglas: avisoReglas);
                },
                r => mensaje = DescribirIncorporacion(r.Resultado, r.AvisoReglas),
                ex => error = ex);
        }
        finally
        {
            IsBusy = false;
        }

        if (mensaje is null)
        {
            InformarErrorDeIncorporacion(error);
        }

        return mensaje;
    }

    private static string DescribirIncorporacion(ResultadoIncorporacionDto resultado, string? avisoReglas)
    {
        string mensaje = resultado.OmitidosYaVinculados > 0
            ? $"{resultado.Incorporados} artículo(s) incorporados a la tienda. {resultado.OmitidosYaVinculados} omitido(s) porque ya estaban en la tienda."
            : $"{resultado.Incorporados} artículo(s) incorporados a la tienda correctamente.";

        return avisoReglas is null ? mensaje : $"{mensaje} {avisoReglas}";
    }

    /// <summary>Sin error, el acceso se descartó al salir de la pantalla: no hay nada que informar.</summary>
    private void InformarErrorDeIncorporacion(Exception? error)
    {
        switch (error)
        {
            case null:
                return;

            case DomainException dominio:
                MensajeError = dominio.Message;
                return;

            default:
                _logger.LogError(error, "Error al incorporar artículos a tienda");
                MensajeError = "Error al incorporar artículos.";
                return;
        }
    }

    // ------------------------------------------------------------------ //
    // Pedir al proveedor (Track 4.3, A-17)                               //
    // ------------------------------------------------------------------ //

    /// <summary>"Pedir" de una fila: es un lote de uno (G4).</summary>
    [RelayCommand]
    private Task PedirItemAsync(CatalogoProveedorDto? item)
    {
        return item is null ? Task.CompletedTask : PedirAsync([item]);
    }

    [RelayCommand]
    private Task PedirSeleccionadosAsync()
    {
        var seleccionados = ItemsCatalogo.Where(i => i.EstaSeleccionado).ToList();
        if (seleccionados.Count == 0)
        {
            MensajeError = "Seleccione al menos un artículo del catálogo para pedir.";
            return Task.CompletedTask;
        }

        return PedirAsync(seleccionados);
    }

    /// <summary>
    /// Recorrido en dos pasos (A-17): si el modal de pedido informa ítems sin incorporar y el usuario elige
    /// incorporarlos, se abre el modal de incorporación y después se vuelve a pedir, ya sin la oferta. Si la
    /// incorporación se cancela, se vuelve igual con los que estaban vinculados.
    /// </summary>
    private async Task PedirAsync(IReadOnlyList<CatalogoProveedorDto> items)
    {
        if (!PuedePedir)
        {
            return;
        }

        MensajeError = null;
        MensajeEstado = null;
        var ids = items.Select(i => i.Id).ToList();

        var resultado = await _flujoPedir.PedirAsync(ids, [], ofrecerIncorporar: true, _cargaCatalogo);

        string? mensajeIncorporacion = null;
        if (resultado.Accion == AccionPedirArticulosEnum.IncorporarPrimero)
        {
            var sinIncorporar = items.Where(i => resultado.IdsSinIncorporar.Contains(i.Id)).ToList();
            mensajeIncorporacion = await IncorporarAsync(sinIncorporar);

            // Si la incorporación falló, su error ya está en pantalla y no se sigue. Si se canceló, se pide igual.
            if (MensajeError is not null)
            {
                return;
            }

            resultado = await _flujoPedir.PedirAsync(ids, [], ofrecerIncorporar: false, _cargaCatalogo);
        }

        MensajeError = resultado.MensajeError;
        var mensajes = new[] { mensajeIncorporacion, resultado.MensajeEstado }.OfType<string>().ToList();
        MensajeEstado = mensajes.Count == 0 ? null : string.Join(" ", mensajes);

        if (resultado.Accion == AccionPedirArticulosEnum.Confirmar)
        {
            foreach (var item in ItemsCatalogo)
            {
                item.EstaSeleccionado = false;
            }
        }

        // Lo incorporado cambia el estado de las filas ("En Tienda"), aunque después se cancele el pedido.
        if (mensajeIncorporacion is not null)
        {
            await CargarCatalogoAsync();
        }
    }

    partial void OnSeleccionarTodosChanged(bool? value)
    {
        if (_isUpdatingSeleccionarTodos || value is null)
        {
            return;
        }

        _isUpdatingSeleccionarTodos = true;
        try
        {
            bool seleccionar = value.Value;
            foreach (var item in ItemsCatalogo)
            {
                item.EstaSeleccionado = seleccionar;
            }
            TieneItemsSeleccionados = ItemsCatalogo.Any(i => i.EstaSeleccionado);
        }
        finally
        {
            _isUpdatingSeleccionarTodos = false;
        }
    }

    /// <summary>
    /// Convierte la fecha UTC de la última planilla a la hora local y calcula su antigüedad en días calendario: una
    /// planilla importada a las 23 h de ayer es "de ayer", aunque en UTC ya sea hoy.
    /// </summary>
    private void MostrarUltimaPlanilla(DateTime? fechaUtc)
    {
        if (fechaUtc is not { } utc)
        {
            DiasDesdeUltimaPlanilla = null;
            FechaUltimaPlanilla = null;
            return;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, _reloj.LocalTimeZone);
        DiasDesdeUltimaPlanilla = Math.Max(0, (_reloj.GetLocalNow().Date - local.Date).Days);
        FechaUltimaPlanilla = local;
    }

    private static string TextoAntiguedad(int dias)
    {
        return dias switch
        {
            0 => "hoy",
            1 => "hace 1 día",
            _ => $"hace {dias} días"
        };
    }

    private void LimpiarItemsCatalogo()
    {
        foreach (var item in ItemsCatalogo)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }
        ItemsCatalogo.Clear();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CatalogoProveedorDto.EstaSeleccionado))
        {
            ActualizarEstadoSeleccion();
        }
    }

    private void ActualizarEstadoSeleccion()
    {
        var total = ItemsCatalogo.Count;
        var seleccionados = ItemsCatalogo.Count(i => i.EstaSeleccionado);
        TieneItemsSeleccionados = seleccionados > 0;

        if (!_isUpdatingSeleccionarTodos)
        {
            _isUpdatingSeleccionarTodos = true;
            try
            {
                SeleccionarTodos = total == 0 ? false : (seleccionados == total ? true : (seleccionados == 0 ? false : null));
            }
            finally
            {
                _isUpdatingSeleccionarTodos = false;
            }
        }
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela la carga en curso para que no use el
    /// DbContext ya descartado ni informe errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _cargaCatalogo.Dispose();
        GC.SuppressFinalize(this);
    }
}
