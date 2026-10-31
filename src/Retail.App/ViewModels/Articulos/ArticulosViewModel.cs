using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Pedidos;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Articulos;

public record class OpcionFiltroDistribuidor(int IdProveedor, string Nombre);
public record class OpcionFiltroStock(FiltroStockArticuloEnum Valor, string Descripcion);

/// <summary>
/// ViewModel principal para la administración del catálogo de artículos y visualización de alertas de stock con push-down a SQL Server (RF-04, RF-08, Ley 8).
/// </summary>
public partial class ArticulosViewModel : ObservableObject, IDisposable
{
    private readonly IInventarioService _inventarioService;
    private readonly IArticuloDialogService _dialogService;
    private readonly IProveedorService? _proveedorService;
    private readonly ILogger<ArticulosViewModel> _logger;
    private readonly IClasificacionDialogService? _clasificacionDialogService;
    private readonly ICurrentUserSession? _session;
    private readonly FlujoPedirArticulos? _flujoPedir;
    private readonly IRubroService? _rubroService;

    // Selección de los combos a restaurar cuando el diálogo de clasificación cambió sus listas.
    private int? _idRubroARestaurar;
    private int? _idCategoriaARestaurar;
    private int? _idMarcaARestaurar;

    // Mientras el rubro reinicia la categoría, el cambio de categoría no dispara su propia carga.
    private bool _cambiandoRubro;

    private List<RubroDto> _cacheRubros = new();
    private List<CategoriaDto> _cacheCategorias = new();
    private List<MarcaDto> _cacheMarcas = new();
    private List<ProveedorDto> _cacheProveedores = new();

    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    // Búsqueda, filtros y paginación comparten esta carga: nunca usan el DbContext de la pantalla al mismo
    // tiempo, solo la vigente actualiza la grilla y al salir de la pantalla se cancela (H-19).
    private readonly CargaSerializada _cargaArticulos;

    public ObservableCollection<ArticuloFila> Articulos { get; } = new();

    public ObservableCollection<RubroDto> RubrosFiltro { get; } = new();

    /// <summary>Las categorías del rubro elegido, o todas si el rubro es "Todos los Rubros".</summary>
    public ObservableCollection<CategoriaDto> CategoriasFiltro { get; } = new();

    public ObservableCollection<MarcaDto> MarcasFiltro { get; } = new();

    public ObservableCollection<OpcionFiltroDistribuidor> DistribuidoresFiltro { get; } = new();

    public ObservableCollection<OpcionFiltroStock> OpcionesStockFiltro { get; } = new();

    [ObservableProperty]
    private ArticuloDto? _articuloSeleccionado;

    /// <summary>Encabezado de la columna de selección: <c>null</c> cuando hay algunas filas marcadas y otras no.</summary>
    [ObservableProperty]
    private bool? _seleccionarTodos = false;

    [ObservableProperty]
    private bool _tieneArticulosSeleccionados;

    /// <summary>Filas marcadas en la página actual; la barra de acciones masivas la muestra.</summary>
    [ObservableProperty]
    private int _cantidadSeleccionados;

    private bool _actualizandoSeleccion;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private int _idRubroFiltro;

    [ObservableProperty]
    private int _idCategoriaFiltro;

    [ObservableProperty]
    private int _idMarcaFiltro;

    [ObservableProperty]
    private int _idDistribuidorFiltro;

    [ObservableProperty]
    private FiltroStockArticuloEnum _filtroStock = FiltroStockArticuloEnum.Todos;

    public bool SoloStockCritico
    {
        get => FiltroStock == FiltroStockArticuloEnum.StockCritico;
        set => FiltroStock = value ? FiltroStockArticuloEnum.StockCritico : FiltroStockArticuloEnum.Todos;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private int _totalArticulos;

    [ObservableProperty]
    private int _totalAlertasStock;

    [ObservableProperty]
    private int _paginaActual = 1;

    [ObservableProperty]
    private int _tamanoPagina = 10;

    [ObservableProperty]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private int _totalRegistrosFiltrados;

    public bool HayArticuloSeleccionado => ArticuloSeleccionado != null;

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalRegistrosFiltrados == 0
        ? "Sin artículos registrados"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistrosFiltrados)} de {TotalRegistrosFiltrados} artículos";

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public bool HayFiltrosActivos =>
        !string.IsNullOrWhiteSpace(TextoBusqueda) ||
        IdRubroFiltro > 0 ||
        IdCategoriaFiltro > 0 ||
        IdMarcaFiltro > 0 ||
        IdDistribuidorFiltro != 0 ||
        FiltroStock != FiltroStockArticuloEnum.Todos;

    /// <summary>
    /// Solo el Gerente y el Encargado administran rubros, categorías y marcas (D-5, RF-04).
    /// </summary>
    public bool PuedeAdministrarClasificacion =>
        _clasificacionDialogService is not null &&
        _session is not null &&
        (_session.EsGerente || _session.EsEncargado);

    /// <summary>Solo el Gerente y el Encargado ven las acciones de pedir (A-14).</summary>
    public bool PuedePedir => _flujoPedir?.PuedePedir == true;

    public ArticulosViewModel(
        IInventarioService inventarioService,
        IArticuloDialogService dialogService,
        IProveedorService? proveedorService = null,
        ILogger<ArticulosViewModel>? logger = null,
        IClasificacionDialogService? clasificacionDialogService = null,
        ICurrentUserSession? session = null,
        FlujoPedirArticulos? flujoPedir = null,
        IRubroService? rubroService = null)
    {
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _proveedorService = proveedorService;
        _logger = logger ?? NullLogger<ArticulosViewModel>.Instance;
        _cargaArticulos = new CargaSerializada(_logger);
        _clasificacionDialogService = clasificacionDialogService;
        _session = session;
        _flujoPedir = flujoPedir;
        _rubroService = rubroService;

        InicializarOpcionesFiltroEstaticas();
    }

    private void InicializarOpcionesFiltroEstaticas()
    {
        OpcionesStockFiltro.Clear();
        OpcionesStockFiltro.Add(new OpcionFiltroStock(FiltroStockArticuloEnum.Todos, "Todos"));
        OpcionesStockFiltro.Add(new OpcionFiltroStock(FiltroStockArticuloEnum.StockCritico, "Stock crítico"));
        OpcionesStockFiltro.Add(new OpcionFiltroStock(FiltroStockArticuloEnum.SinStock, "Sin stock"));
    }

    [RelayCommand(CanExecute = nameof(HayFiltrosActivos))]
    public void LimpiarFiltros()
    {
        TextoBusqueda = string.Empty;
        IdRubroFiltro = 0;
        IdCategoriaFiltro = 0;
        IdMarcaFiltro = 0;
        IdDistribuidorFiltro = 0;
        FiltroStock = FiltroStockArticuloEnum.Todos;
        PaginaActual = 1;
        _ = CargarArticulosAsync();
    }

    partial void OnArticuloSeleccionadoChanged(ArticuloDto? value)
    {
        OnPropertyChanged(nameof(HayArticuloSeleccionado));
    }

    partial void OnIsBusyChanged(bool value)
    {
        AdministrarClasificacionCommand.NotifyCanExecuteChanged();
    }

    private bool PuedeAbrirAdministracion => PuedeAdministrarClasificacion && !IsBusy;

    /// <summary>
    /// Abre el diálogo de administración de rubros, categorías y marcas. Si hubo cambios, recarga los combos
    /// de filtro, que solo se llenan cuando están vacíos (Track 6.5).
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeAbrirAdministracion))]
    private async Task AdministrarClasificacionAsync()
    {
        if (!PuedeAdministrarClasificacion)
        {
            return;
        }

        if (_clasificacionDialogService!.AbrirAdministracion())
        {
            await RecargarClasificacionAsync();
        }
    }

    /// <summary>
    /// Vacía las cachés y los combos de rubros, categorías y marcas para que la carga habitual los vuelva a llenar,
    /// y conserva la selección de cada combo si el elemento sigue existiendo (si no, vuelve a "Todos").
    /// </summary>
    private Task RecargarClasificacionAsync()
    {
        _idRubroARestaurar = IdRubroFiltro;
        _idCategoriaARestaurar = IdCategoriaFiltro;
        _idMarcaARestaurar = IdMarcaFiltro;
        _cacheRubros = new List<RubroDto>();
        _cacheCategorias = new List<CategoriaDto>();
        _cacheMarcas = new List<MarcaDto>();
        RubrosFiltro.Clear();
        CategoriasFiltro.Clear();
        MarcasFiltro.Clear();

        return CargarArticulosAsync();
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        PaginaActual = 1;
        ActualizarEstadoFiltros();
        _ = CargarArticulosConEsperaAsync(EsperaBusqueda);
    }

    /// <summary>
    /// Elegir un rubro acota las opciones de categoría a las suyas y vuelve a "Todas las Categorías", que pasa a
    /// significar "todas las del rubro". Los dos cambios disparan una sola carga.
    /// </summary>
    partial void OnIdRubroFiltroChanged(int value)
    {
        _idCategoriaARestaurar = null;
        _cambiandoRubro = true;
        try
        {
            IdCategoriaFiltro = 0;
            EjecutarEnDispatcher(LlenarCategoriasFiltro);
        }
        finally
        {
            _cambiandoRubro = false;
        }

        // El combo de categorías se vació y se volvió a llenar: se le vuelve a indicar cuál está elegida.
        OnPropertyChanged(nameof(IdCategoriaFiltro));
        PaginaActual = 1;
        ActualizarEstadoFiltros();
        _ = CargarArticulosAsync();
    }

    /// <summary>
    /// Las opciones salen del diccionario de categorías que la pantalla ya cargó (cada una trae su rubro): recortar
    /// unas decenas de opciones de un combo no es una consulta de datos. Los artículos sí se filtran en SQL (Ley 8).
    /// </summary>
    private void LlenarCategoriasFiltro()
    {
        CategoriasFiltro.Clear();
        CategoriasFiltro.Add(new CategoriaDto { IdCategoria = 0, NombreCategoria = "Todas las Categorías" });
        foreach (var categoria in _cacheCategorias.Where(c => IdRubroFiltro == 0 || c.IdRubro == IdRubroFiltro))
        {
            CategoriasFiltro.Add(categoria);
        }
    }

    partial void OnIdCategoriaFiltroChanged(int value)
    {
        if (_cambiandoRubro)
        {
            return;
        }

        PaginaActual = 1;
        ActualizarEstadoFiltros();
        _ = CargarArticulosAsync();
    }

    partial void OnIdMarcaFiltroChanged(int value)
    {
        PaginaActual = 1;
        ActualizarEstadoFiltros();
        _ = CargarArticulosAsync();
    }

    partial void OnIdDistribuidorFiltroChanged(int value)
    {
        PaginaActual = 1;
        ActualizarEstadoFiltros();
        _ = CargarArticulosAsync();
    }

    partial void OnFiltroStockChanged(FiltroStockArticuloEnum value)
    {
        PaginaActual = 1;
        OnPropertyChanged(nameof(SoloStockCritico));
        ActualizarEstadoFiltros();
        _ = CargarArticulosAsync();
    }

    private void ActualizarEstadoFiltros()
    {
        OnPropertyChanged(nameof(HayFiltrosActivos));
        LimpiarFiltrosCommand.NotifyCanExecuteChanged();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        _ = CargarArticulosAsync();
    }

    [RelayCommand]
    public Task CargarArticulosAsync(CancellationToken cancellationToken = default)
    {
        return CargarArticulosConEsperaAsync(TimeSpan.Zero, cancellationToken);
    }

    private async Task CargarArticulosConEsperaAsync(TimeSpan espera, CancellationToken cancellationToken = default)
    {
        bool cargarFiltros = CategoriasFiltro.Count == 0 || MarcasFiltro.Count == 0 || DistribuidoresFiltro.Count == 0 ||
            (_rubroService != null && RubrosFiltro.Count == 0);
        var consulta = new ConsultaArticulosDto
        {
            TerminoBusqueda = TextoBusqueda,
            IdRubro = IdRubroFiltro > 0 ? IdRubroFiltro : null,
            IdCategoria = IdCategoriaFiltro > 0 ? IdCategoriaFiltro : null,
            IdMarca = IdMarcaFiltro > 0 ? IdMarcaFiltro : null,
            IdProveedor = IdDistribuidorFiltro != 0 ? IdDistribuidorFiltro : null,
            FiltroStock = FiltroStock,
            Pagina = PaginaActual,
            TamanoPagina = TamanoPagina
        };

        IsBusy = true;
        MensajeError = null;
        MensajeEstado = "Cargando catálogo de artículos...";

        await _cargaArticulos.EjecutarAsync(
            async token =>
            {
                // Las consultas de una misma carga son secuenciales: nunca se solapan sobre el DbContext.
                IReadOnlyList<RubroDto>? rubros = null;
                IReadOnlyList<CategoriaDto>? categorias = null;
                IReadOnlyList<MarcaDto>? marcas = null;
                IReadOnlyList<ProveedorDto>? proveedores = null;

                if (cargarFiltros)
                {
                    if (_rubroService != null)
                    {
                        rubros = await _rubroService.ListarAsync(null, token);
                    }

                    categorias = await _inventarioService.ListarCategoriasAsync(token);
                    marcas = await _inventarioService.ListarMarcasAsync(token);
                    if (_proveedorService != null)
                    {
                        proveedores = await _proveedorService.ListarProveedoresAsync(token);
                    }
                }

                var pagina = await Task.Run(() => _inventarioService.ListarArticulosPaginadosAsync(consulta, token), token);
                return (Rubros: rubros, Categorias: categorias, Marcas: marcas, Proveedores: proveedores, Pagina: pagina);
            },
            resultado => MostrarArticulos(resultado.Rubros, resultado.Categorias, resultado.Marcas, resultado.Proveedores, resultado.Pagina),
            ex =>
            {
                _logger.LogError(ex, "Error al cargar el catálogo de artículos: {Mensaje}", ex.Message);
                MensajeError = $"Error al cargar catálogo: {ex.Message}";
                _dialogService.MostrarError("Error de Carga", ex.Message);
            },
            espera,
            cancellationToken);

        IsBusy = _cargaArticulos.EnCurso;
    }

    private void MostrarArticulos(
        IReadOnlyList<RubroDto>? rubros,
        IReadOnlyList<CategoriaDto>? categorias,
        IReadOnlyList<MarcaDto>? marcas,
        IReadOnlyList<ProveedorDto>? proveedores,
        ArticulosPaginadosDto resultado)
    {
        if (categorias is not null && marcas is not null)
        {
            _cacheCategorias = categorias.ToList();
            _cacheMarcas = marcas.ToList();
            if (rubros is not null)
            {
                _cacheRubros = rubros.ToList();
            }
            if (proveedores is not null)
            {
                _cacheProveedores = proveedores.ToList();
            }

            EjecutarEnDispatcher(() =>
            {
                // Primero el rubro: si el que estaba elegido ya no existe, volver a "Todos" reinicia la categoría.
                if (RubrosFiltro.Count == 0 && _rubroService != null)
                {
                    RubrosFiltro.Add(new RubroDto { IdRubro = 0, NombreRubro = "Todos los Rubros" });
                    foreach (var rubro in _cacheRubros)
                    {
                        RubrosFiltro.Add(rubro);
                    }

                    if (_idRubroARestaurar is int idRubro)
                    {
                        _idRubroARestaurar = null;
                        IdRubroFiltro = RubrosFiltro.Any(r => r.IdRubro == idRubro) ? idRubro : 0;
                    }
                }

                if (CategoriasFiltro.Count == 0)
                {
                    LlenarCategoriasFiltro();

                    if (_idCategoriaARestaurar is int idCategoria)
                    {
                        _idCategoriaARestaurar = null;
                        IdCategoriaFiltro = CategoriasFiltro.Any(c => c.IdCategoria == idCategoria) ? idCategoria : 0;
                    }
                }

                if (MarcasFiltro.Count == 0)
                {
                    MarcasFiltro.Clear();
                    MarcasFiltro.Add(new MarcaDto { IdMarca = 0, NombreMarca = "Todas las Marcas" });
                    foreach (var m in _cacheMarcas)
                    {
                        MarcasFiltro.Add(m);
                    }

                    if (_idMarcaARestaurar is int idMarca)
                    {
                        _idMarcaARestaurar = null;
                        IdMarcaFiltro = MarcasFiltro.Any(m => m.IdMarca == idMarca) ? idMarca : 0;
                    }
                }

                if (DistribuidoresFiltro.Count == 0)
                {
                    DistribuidoresFiltro.Clear();
                    DistribuidoresFiltro.Add(new OpcionFiltroDistribuidor(0, "Todos"));
                    DistribuidoresFiltro.Add(new OpcionFiltroDistribuidor(-1, "Ninguno (o manual)"));
                    foreach (var prov in _cacheProveedores)
                    {
                        DistribuidoresFiltro.Add(new OpcionFiltroDistribuidor(prov.IdProveedor, prov.RazonSocial));
                    }
                }
            });
        }

        EjecutarEnDispatcher(() =>
        {
            LimpiarFilas();
            foreach (var art in resultado.Items)
            {
                var fila = new ArticuloFila(art);
                fila.PropertyChanged += Fila_PropertyChanged;
                Articulos.Add(fila);
            }

            ActualizarEstadoSeleccion();
        });

        TotalArticulos = resultado.TotalArticulos;
        TotalAlertasStock = resultado.TotalAlertasStock;
        TotalRegistrosFiltrados = resultado.TotalRegistros;
        TotalPaginas = resultado.TotalPaginas;

        OnPropertyChanged(nameof(PuedeRetrocederPagina));
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));

        MensajeEstado = $"Mostrando {Articulos.Count} de {TotalArticulos} artículos ({TotalAlertasStock} con alerta de stock).";
    }

    [RelayCommand]
    public async Task NuevoArticuloAsync()
    {
        try
        {
            if (_cacheCategorias.Count == 0 || _cacheMarcas.Count == 0)
            {
                _cacheCategorias = (await _inventarioService.ListarCategoriasAsync()).ToList();
                _cacheMarcas = (await _inventarioService.ListarMarcasAsync()).ToList();
            }

            var nuevoDto = _dialogService.MostrarDialogoCrear(
                _cacheCategorias,
                _cacheMarcas,
                async (crearDto) =>
                {
                    await _inventarioService.CrearArticuloAsync(crearDto);
                });

            if (nuevoDto != null)
            {
                _dialogService.MostrarInformacion("Catálogo de Artículos", "El artículo fue incorporado al catálogo exitosamente.");
                await CargarArticulosAsync();
            }
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Validación de negocio al crear artículo: {Mensaje}", dex.Message);
            _dialogService.MostrarError("Regla de Catálogo", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear artículo: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error Inesperado", ex.Message);
        }
    }

    [RelayCommand]
    public async Task EditarArticuloAsync(ArticuloDto? param)
    {
        var target = param ?? ArticuloSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarInformacion("Catálogo", "Seleccione un artículo de la grilla para modificar.");
            return;
        }

        try
        {
            if (_cacheCategorias.Count == 0 || _cacheMarcas.Count == 0)
            {
                _cacheCategorias = (await _inventarioService.ListarCategoriasAsync()).ToList();
                _cacheMarcas = (await _inventarioService.ListarMarcasAsync()).ToList();
            }

            // Se leen al abrir y no se cachean: otra pantalla pudo cambiar las fuentes (A-19). Una presentación derivada
            // no tiene fuentes propias (RF-21).
            IReadOnlyList<FuenteArticuloDto> fuentes = target.EsDerivado
                ? []
                : await _inventarioService.ObtenerFuentesDeArticuloAsync(target.IdArticulo);

            var modificadoDto = _dialogService.MostrarDialogoModificar(
                target,
                fuentes,
                _cacheCategorias,
                _cacheMarcas,
                async (actDto) =>
                {
                    await _inventarioService.ActualizarArticuloAsync(actDto);
                });

            if (modificadoDto != null)
            {
                _dialogService.MostrarInformacion("Catálogo de Artículos", "El artículo fue actualizado exitosamente.");
                await CargarArticulosAsync();
            }
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Validación de negocio al modificar artículo: {Mensaje}", dex.Message);
            _dialogService.MostrarError("Regla de Catálogo", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al modificar artículo: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error Inesperado", ex.Message);
        }
    }

    [RelayCommand]
    public async Task BajaArticuloAsync(ArticuloDto? param)
    {
        var target = param ?? ArticuloSeleccionado;
        if (target == null)
        {
            _dialogService.MostrarInformacion("Catálogo", "Seleccione un artículo de la grilla para dar de baja.");
            return;
        }

        var confirmacion = _dialogService.Confirmar(
            "Confirmar Baja de Artículo",
            $"¿Está seguro de dar de baja el artículo '{target.Descripcion}'?\nEl registro será archivado lógicamente (Soft Delete).");

        if (!confirmacion)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _inventarioService.BajaArticuloAsync(target.IdArticulo);
            _dialogService.MostrarInformacion("Baja Exitosa", $"El artículo '{target.Descripcion}' fue dado de baja correctamente.");
            await CargarArticulosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al dar de baja el artículo {Id}: {Mensaje}", target.IdArticulo, ex.Message);
            _dialogService.MostrarError("Error de Baja", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Alta de una presentación derivada del artículo indicado, por ejemplo la unidad suelta de un pack (RF-21).
    /// </summary>
    [RelayCommand]
    public async Task CrearPresentacionAsync(ArticuloDto? param)
    {
        var origen = param ?? ArticuloSeleccionado;
        if (origen == null)
        {
            _dialogService.MostrarInformacion("Presentaciones", "Seleccione el artículo de compra (por ejemplo, el pack) para crear su presentación.");
            return;
        }

        // Aviso temprano con las mismas reglas que valida el Dominio (un solo nivel, sin servicios).
        if (origen.EsDerivado)
        {
            _dialogService.MostrarInformacion("Presentaciones", $"'{origen.Descripcion}' ya es una presentación derivada: no puede ser origen de otra.");
            return;
        }

        if (origen.EsServicio)
        {
            _dialogService.MostrarInformacion("Presentaciones", "Un servicio no maneja stock y no puede tener presentaciones.");
            return;
        }

        try
        {
            var creada = _dialogService.MostrarDialogoCrearPresentacion(
                origen,
                async (dto) =>
                {
                    await _inventarioService.CrearPresentacionAsync(dto);
                });

            if (creada != null)
            {
                _dialogService.MostrarInformacion(
                    "Presentaciones",
                    $"Se creó la presentación '{creada.Descripcion}'. Para darle stock, fraccione el artículo de origen.");
                await CargarArticulosAsync();
            }
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Validación de negocio al crear presentación: {Mensaje}", dex.Message);
            _dialogService.MostrarError("Regla de Catálogo", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear presentación: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error Inesperado", ex.Message);
        }
    }

    /// <summary>
    /// Abre unidades del artículo de origen y las suma a la presentación indicada (RF-21).
    /// </summary>
    [RelayCommand]
    public async Task FraccionarAsync(ArticuloDto? param)
    {
        var derivado = param ?? ArticuloSeleccionado;
        if (derivado == null)
        {
            _dialogService.MostrarInformacion("Fraccionamiento", "Seleccione la presentación (por ejemplo, la unidad suelta) que quiere reponer.");
            return;
        }

        if (derivado.IdArticuloOrigen is not int idArticuloOrigen)
        {
            _dialogService.MostrarInformacion("Fraccionamiento", $"'{derivado.Descripcion}' no es una presentación derivada: no tiene un artículo de origen para fraccionar.");
            return;
        }

        try
        {
            // El diálogo necesita el stock vigente del origen para la vista previa y el aviso temprano.
            var origen = await _inventarioService.ObtenerPorIdAsync(idArticuloOrigen);
            if (origen == null)
            {
                _dialogService.MostrarError("Fraccionamiento", $"El artículo de origen de '{derivado.Descripcion}' fue dado de baja.");
                return;
            }

            var unidades = _dialogService.MostrarDialogoFraccionar(
                derivado,
                origen,
                (dto) => _inventarioService.FraccionarAsync(dto));

            if (unidades != null)
            {
                _dialogService.MostrarInformacion(
                    "Fraccionamiento",
                    $"Se sumaron {unidades} unidad(es) a '{derivado.Descripcion}'.");
                await CargarArticulosAsync();
            }
        }
        catch (DomainException dex)
        {
            _logger.LogWarning(dex, "Validación de negocio al fraccionar: {Mensaje}", dex.Message);
            _dialogService.MostrarError("Regla de Stock", dex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al fraccionar: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error Inesperado", ex.Message);
        }
    }

    // ------------------------------------------------------------------ //
    // Selección y pedido al proveedor (Track 4.3, A-17)                  //
    // ------------------------------------------------------------------ //

    /// <summary>"Pedir" de una fila: es un lote de uno (G4).</summary>
    [RelayCommand]
    private Task PedirArticuloAsync(ArticuloFila? fila)
    {
        return fila is null ? Task.CompletedTask : PedirAsync([fila]);
    }

    [RelayCommand]
    private Task PedirSeleccionadosAsync()
    {
        var seleccionadas = Articulos.Where(f => f.EstaSeleccionado).ToList();
        return seleccionadas.Count == 0 ? Task.CompletedTask : PedirAsync(seleccionadas);
    }

    /// <summary>
    /// Pide los artículos que tienen alguna fuente, a la principal o, sin ella, a la más barata (paso 3c); los demás se
    /// informan en el modal con su motivo. Aquí no se ofrece incorporar: un artículo ya está en la tienda, lo que le
    /// falta es la fuente (A-17).
    /// </summary>
    private async Task PedirAsync(IReadOnlyList<ArticuloFila> filas)
    {
        if (_flujoPedir is null || !PuedePedir)
        {
            return;
        }

        var idsCatalogo = filas
            .Where(f => f.SePuedePedir)
            .Select(f => f.IdFuenteParaPedir!.Value)
            .Distinct()
            .ToList();
        var avisosPrevios = filas
            .Where(f => !f.SePuedePedir)
            .Select(f => $"{f.Articulo.Descripcion}: {f.MotivoNoSePuedePedir}")
            .ToList();

        // Filtrar por un distribuidor es elegir a quién reponer: el modal arranca en su fuente (Track 4.3, K6).
        var resultado = await _flujoPedir.PedirAsync(
            idsCatalogo,
            avisosPrevios,
            ofrecerIncorporar: false,
            _cargaArticulos,
            idProveedorPreferido: IdDistribuidorFiltro > 0 ? IdDistribuidorFiltro : null);

        if (resultado.MensajeError is not null)
        {
            _dialogService.MostrarError("No se pudo pedir", resultado.MensajeError);
            return;
        }

        if (resultado.Accion == AccionPedirArticulosEnum.Confirmar)
        {
            foreach (var fila in Articulos)
            {
                fila.EstaSeleccionado = false;
            }

            _dialogService.MostrarInformacion("Pedido a proveedor", resultado.MensajeEstado!);
        }
    }

    /// <summary>Quita la marca de todas las filas de la página ("Cancelar selección" de la barra de acciones masivas).</summary>
    [RelayCommand]
    private void LimpiarSeleccion()
    {
        SeleccionarTodos = false;
    }

    partial void OnSeleccionarTodosChanged(bool? value)
    {
        if (_actualizandoSeleccion || value is null)
        {
            return;
        }

        _actualizandoSeleccion = true;
        try
        {
            foreach (var fila in Articulos)
            {
                fila.EstaSeleccionado = value.Value;
            }
        }
        finally
        {
            _actualizandoSeleccion = false;
        }

        ActualizarEstadoSeleccion();
    }

    private void Fila_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ArticuloFila.EstaSeleccionado) && !_actualizandoSeleccion)
        {
            ActualizarEstadoSeleccion();
        }
    }

    /// <summary>Recalcula el encabezado (todas, ninguna o algunas) y si hay algo para pedir.</summary>
    private void ActualizarEstadoSeleccion()
    {
        int seleccionadas = Articulos.Count(f => f.EstaSeleccionado);
        TieneArticulosSeleccionados = seleccionadas > 0;
        CantidadSeleccionados = seleccionadas;

        _actualizandoSeleccion = true;
        try
        {
            SeleccionarTodos = seleccionadas == 0 ? false : seleccionadas == Articulos.Count ? true : null;
        }
        finally
        {
            _actualizandoSeleccion = false;
        }
    }

    private void LimpiarFilas()
    {
        foreach (var fila in Articulos)
        {
            fila.PropertyChanged -= Fila_PropertyChanged;
        }

        Articulos.Clear();
    }

    [RelayCommand]
    public void AlternarSoloStockCritico()
    {
        SoloStockCritico = !SoloStockCritico;
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarArticulosAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarArticulosAsync();
        }
    }

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarArticulosAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarArticulosAsync();
        }
    }

    private static void EjecutarEnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela la carga en curso para que no use el
    /// DbContext ya descartado ni muestre errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _cargaArticulos.Dispose();
        GC.SuppressFinalize(this);
    }
}
