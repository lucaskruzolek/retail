using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Converters;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Reportes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Reportes;

/// <summary>
/// ViewModel del tablero gerencial y los reportes (IU-11, RF-24 a RF-26). Consulta cada reporte por medio de
/// <see cref="IReporteService"/> (que autoriza al Gerente, valida los filtros y convierte las fechas), guarda el
/// último resultado de cada reporte y lo presenta en tarjetas, gráficos y grillas. Exportar usa el resultado guardado,
/// sin volver a consultar la base.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private const string TextoTodas = "Todas";
    private const string TextoTodos = "Todos";
    private const string MensajeErrorInesperado =
        "Ocurrió un error inesperado. Intente nuevamente; si el problema continúa, avise al administrador del sistema.";

    // Serializa las consultas de la pantalla sobre su DbContext y descarta las respuestas viejas (H-18, H-19):
    // si el Gerente cambia de reporte mientras se consulta otro, solo se aplica la consulta vigente.
    private readonly CargaSerializada _carga = new();

    // Último resultado consultado de cada reporte. Al volver a una pestaña ya consultada se muestra este resultado en
    // lugar de consultar de nuevo.
    private readonly Dictionary<TipoReporteEnum, ResultadoReporteDto> _resultados = new();

    private readonly IReporteService _reporteService;
    private readonly IExportacionReportesService _exportacionService;
    private readonly IInventarioService _inventarioService;
    private readonly IUsuarioService _usuarioService;
    private readonly ICurrentUserSession _session;
    private readonly IReporteDialogService _dialogService;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DashboardViewModel> _logger;

    private bool _listasCargadas;
    private Task _consultaAutomatica = Task.CompletedTask;

    public DashboardViewModel(
        IReporteService reporteService,
        IExportacionReportesService exportacionService,
        IInventarioService inventarioService,
        IUsuarioService usuarioService,
        ICurrentUserSession session,
        IReporteDialogService dialogService,
        TimeProvider reloj,
        ILogger<DashboardViewModel>? logger = null)
    {
        _reporteService = reporteService ?? throw new ArgumentNullException(nameof(reporteService));
        _exportacionService = exportacionService ?? throw new ArgumentNullException(nameof(exportacionService));
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
        _usuarioService = usuarioService ?? throw new ArgumentNullException(nameof(usuarioService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _logger = logger ?? NullLogger<DashboardViewModel>.Instance;

        var hoy = Hoy;
        FechaMaxima = hoy.ToDateTime(TimeOnly.MinValue);
        Hasta = FechaMaxima;
        Desde = new DateTime(hoy.Year, hoy.Month, 1);

        Categorias = [new OpcionFiltro(null, TextoTodas)];
        Marcas = [new OpcionFiltro(null, TextoTodas)];
        Cajeros = [new OpcionFiltro(null, TextoTodos)];
        _categoriaSeleccionada = Categorias[0];
        _marcaSeleccionada = Marcas[0];
        _cajeroSeleccionado = Cajeros[0];

        ActualizarPresentacion();
    }

    // ---------------------------------------------------------------- Reporte seleccionado y filtros

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarFechas))]
    [NotifyPropertyChangedFor(nameof(MostrarAgrupacion))]
    [NotifyPropertyChangedFor(nameof(MostrarCajero))]
    [NotifyPropertyChangedFor(nameof(MostrarCategoriaYMarca))]
    [NotifyPropertyChangedFor(nameof(MostrarRankingFiltros))]
    [NotifyPropertyChangedFor(nameof(MostrarAgruparPor))]
    [NotifyPropertyChangedFor(nameof(MostrarDias))]
    private TipoReporteEnum _reporteSeleccionado = TipoReporteEnum.Tablero;

    [ObservableProperty]
    private DateTime? _desde;

    [ObservableProperty]
    private DateTime? _hasta;

    [ObservableProperty]
    private AgrupacionTemporalEnum _agrupacion = AgrupacionTemporalEnum.Dia;

    [ObservableProperty]
    private AgrupacionRentabilidadEnum _agrupacionRentabilidad = AgrupacionRentabilidadEnum.Categoria;

    [ObservableProperty]
    private CriterioRankingEnum _criterio = CriterioRankingEnum.Unidades;

    [ObservableProperty]
    private int _cantidad = 10;

    [ObservableProperty]
    private int _dias = 90;

    [ObservableProperty]
    private OpcionFiltro? _categoriaSeleccionada;

    [ObservableProperty]
    private OpcionFiltro? _marcaSeleccionada;

    [ObservableProperty]
    private OpcionFiltro? _cajeroSeleccionado;

    /// <summary>Último día elegible: los reportes no admiten fechas futuras.</summary>
    public DateTime FechaMaxima { get; }

    /// <summary>
    /// Primer día elegible (D-32): el de la primera venta registrada o, sin ventas, el día 1 del mes en curso. Antes de
    /// consultarlo no hay límite.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MesAnteriorCommand))]
    [NotifyPropertyChangedFor(nameof(TooltipMesAnterior))]
    private DateTime _fechaMinima = DateTime.MinValue;

    /// <summary>"Mes anterior" solo tiene sentido si ese mes termina en o después de la primera venta (D-32).</summary>
    public bool PuedeMesAnterior => new DateOnly(Hoy.Year, Hoy.Month, 1).AddDays(-1).ToDateTime(TimeOnly.MinValue) >= FechaMinima;

    public string? TooltipMesAnterior => PuedeMesAnterior ? null : "No hay ventas registradas en el mes anterior";

    public ObservableCollection<OpcionFiltro> Categorias { get; }

    public ObservableCollection<OpcionFiltro> Marcas { get; }

    public ObservableCollection<OpcionFiltro> Cajeros { get; }

    public IReadOnlyList<AgrupacionTemporalEnum> Agrupaciones { get; } = Enum.GetValues<AgrupacionTemporalEnum>();

    public IReadOnlyList<AgrupacionRentabilidadEnum> AgrupacionesRentabilidad { get; } = Enum.GetValues<AgrupacionRentabilidadEnum>();

    public IReadOnlyList<CriterioRankingEnum> Criterios { get; } = Enum.GetValues<CriterioRankingEnum>();

    public int? IdCategoria => CategoriaSeleccionada?.Id;

    public int? IdMarca => MarcaSeleccionada?.Id;

    public int? IdUsuario => CajeroSeleccionado?.Id;

    // Qué filtros muestra cada reporte. Stock crítico y sin movimiento son una foto de hoy: no tienen fechas.
    public bool MostrarFechas => ReporteSeleccionado is TipoReporteEnum.Tablero or TipoReporteEnum.VentasPorPeriodo
        or TipoReporteEnum.Ranking or TipoReporteEnum.Rentabilidad;

    public bool MostrarAgrupacion => ReporteSeleccionado == TipoReporteEnum.VentasPorPeriodo;

    public bool MostrarCajero => ReporteSeleccionado == TipoReporteEnum.VentasPorPeriodo;

    public bool MostrarCategoriaYMarca => ReporteSeleccionado is TipoReporteEnum.Ranking or TipoReporteEnum.Rentabilidad
        or TipoReporteEnum.StockCritico or TipoReporteEnum.SinMovimiento;

    public bool MostrarRankingFiltros => ReporteSeleccionado == TipoReporteEnum.Ranking;

    public bool MostrarAgruparPor => ReporteSeleccionado == TipoReporteEnum.Rentabilidad;

    public bool MostrarDias => ReporteSeleccionado == TipoReporteEnum.SinMovimiento;

    // ---------------------------------------------------------------- Estado de la pantalla

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConsultarCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarExcelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    private bool _estaCargando;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportarExcelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    private bool _estaExportando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>Resultado del reporte visible, o <c>null</c> si todavía no se consultó.</summary>
    [ObservableProperty]
    private ResultadoReporteDto? _resultadoActual;

    /// <summary>El reporte visible se consultó y no tiene filas (D-25). El tablero nunca se considera vacío.</summary>
    [ObservableProperty]
    private bool _estaVacio;

    [ObservableProperty]
    private string _mensajeVacio = string.Empty;

    /// <summary>El reporte visible todavía no se consultó.</summary>
    [ObservableProperty]
    private bool _sinConsultar = true;

    [ObservableProperty]
    private bool _mostrarTablero;

    [ObservableProperty]
    private bool _mostrarVentasPorPeriodo;

    [ObservableProperty]
    private bool _mostrarRanking;

    [ObservableProperty]
    private bool _mostrarRentabilidad;

    [ObservableProperty]
    private bool _mostrarStockCritico;

    [ObservableProperty]
    private bool _mostrarSinMovimiento;

    /// <summary>Hay un gráfico al lado de la grilla: solo rentabilidad (ventas por período, D-34, y ranking, D-33, son tablas).</summary>
    [ObservableProperty]
    private bool _mostrarGraficoReporte;

    /// <summary>Se puede exportar el reporte visible: tiene resultado y no está vacío (D-25).</summary>
    public bool PuedeExportarResultado => ResultadoActual is not null && !EstaVacio;

    public string? TooltipExportar => PuedeExportarResultado ? null : "No hay datos para exportar";

    // ---------------------------------------------------------------- Tablero

    [ObservableProperty]
    private string _totalVendidoTexto = string.Empty;

    [ObservableProperty]
    private VariacionIndicador _totalVendidoVariacion = FormatoReporte.Variacion(null);

    [ObservableProperty]
    private string _cantidadVentasTexto = string.Empty;

    [ObservableProperty]
    private VariacionIndicador _cantidadVentasVariacion = FormatoReporte.Variacion(null);

    [ObservableProperty]
    private string _margenBrutoTexto = string.Empty;


    [ObservableProperty]
    private VariacionIndicador _margenBrutoVariacion = FormatoReporte.Variacion(null);


    [ObservableProperty]
    private string _valorizacionTexto = string.Empty;

    [ObservableProperty]
    private string _bajoMinimoTexto = string.Empty;

    [ObservableProperty]
    private bool _hayBajoMinimo;

    /// <summary>Contra qué fechas se comparan las tarjetas del tablero (D-31).</summary>
    [ObservableProperty]
    private string _textoComparacion = string.Empty;

    // ---------------------------------------------------------------- Gráficos

    [ObservableProperty]
    private GraficoPantalla _graficoEvolucion = GraficoPantalla.Vacio;

    [ObservableProperty]
    private GraficoPantalla _graficoMargenCategoria = GraficoPantalla.Vacio;

    [ObservableProperty]
    private string _tituloMargenCategoria = "Margen bruto por categoría";

    /// <summary>Gráfico del reporte visible cuando no es el tablero.</summary>
    [ObservableProperty]
    private GraficoPantalla _graficoReporte = GraficoPantalla.Vacio;

    [ObservableProperty]
    private string _tituloGraficoReporte = string.Empty;

    // ---------------------------------------------------------------- Grillas y totales al pie

    [ObservableProperty]
    private IReadOnlyList<PuntoVentasDto> _filasVentasPorPeriodo = [];

    [ObservableProperty]
    private IReadOnlyList<RankingArticuloDto> _masVendidos = [];

    [ObservableProperty]
    private IReadOnlyList<RankingArticuloDto> _menosVendidos = [];

    [ObservableProperty]
    private IReadOnlyList<RentabilidadFilaDto> _filasRentabilidad = [];

    [ObservableProperty]
    private IReadOnlyList<StockCriticoFilaDto> _filasStockCritico = [];

    [ObservableProperty]
    private IReadOnlyList<ArticuloSinMovimientoDto> _filasSinMovimiento = [];

    /// <summary>
    /// Totales de Ventas por período y fila Total de Rentabilidad: la vista los muestra en una fila alineada bajo las
    /// columnas de su grilla, con el mismo formato que las celdas.
    /// </summary>
    [ObservableProperty]
    private VentasPorPeriodoDto? _totalesVentas;

    [ObservableProperty]
    private RentabilidadFilaDto? _totalRentabilidad;

    /// <summary>Totales del reporte visible en una franja al pie (stock crítico y sin movimiento).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneResumen))]
    private IReadOnlyList<DatoResumen> _resumen = [];

    public bool TieneResumen => Resumen.Count > 0;

    // ---------------------------------------------------------------- Consulta

    private DateOnly Hoy => DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);

    private int IdActor => _session.IdUsuario ?? 0;

    /// <summary>
    /// Carga las listas de los combos una sola vez y consulta el tablero del mes en curso. Si ya hay un tablero
    /// consultado (se volvió a mostrar la misma pantalla), no hace nada.
    /// </summary>
    [RelayCommand]
    public async Task CargarAsync()
    {
        if (_resultados.ContainsKey(TipoReporteEnum.Tablero) && _listasCargadas)
        {
            return;
        }

        EstaCargando = true;
        MensajeError = null;

        if (!_listasCargadas)
        {
            await CargarListasAsync();
        }

        // Antes del tablero: el calendario y la consulta necesitan conocer el límite inferior (D-32).
        await CargarFechaMinimaAsync();
        await ConsultarReporteAsync(TipoReporteEnum.Tablero);
    }

    [RelayCommand(CanExecute = nameof(PuedeConsultar))]
    public async Task ConsultarAsync()
    {
        await ConsultarReporteAsync(ReporteSeleccionado);
    }

    [RelayCommand]
    public void EsteMes()
    {
        var hoy = Hoy;
        Desde = LimitarAlMinimo(new DateTime(hoy.Year, hoy.Month, 1));
        Hasta = hoy.ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand(CanExecute = nameof(PuedeMesAnterior))]
    public void MesAnterior()
    {
        var primeroDeEsteMes = new DateOnly(Hoy.Year, Hoy.Month, 1);
        var primeroDelAnterior = primeroDeEsteMes.AddMonths(-1);

        Desde = LimitarAlMinimo(primeroDelAnterior.ToDateTime(TimeOnly.MinValue));
        Hasta = primeroDeEsteMes.AddDays(-1).ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    public void EsteAnio()
    {
        var hoy = Hoy;
        Desde = LimitarAlMinimo(new DateTime(hoy.Year, 1, 1));
        Hasta = hoy.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>
    /// Espera la consulta que se disparó sola al cambiar de reporte. Las pruebas y quien necesite saber cuándo terminó
    /// la usan; el enlace de la pantalla no.
    /// </summary>
    public Task EsperarConsultaAutomaticaAsync() => _consultaAutomatica;

    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }

    partial void OnReporteSeleccionadoChanged(TipoReporteEnum value)
    {
        MensajeError = null;
        ActualizarPresentacion();

        // Una pestaña que nunca se consultó se consulta sola la primera vez; una ya consultada muestra su último resultado.
        // Antes de la carga inicial no se consulta: CargarAsync ya consulta el tablero.
        if (_listasCargadas)
        {
            _consultaAutomatica = ConsultarSiHaceFaltaAsync(value);
        }
    }

    partial void OnCategoriaSeleccionadaChanged(OpcionFiltro? value) => OnPropertyChanged(nameof(IdCategoria));

    partial void OnMarcaSeleccionadaChanged(OpcionFiltro? value) => OnPropertyChanged(nameof(IdMarca));

    partial void OnCajeroSeleccionadoChanged(OpcionFiltro? value) => OnPropertyChanged(nameof(IdUsuario));

    private bool PuedeConsultar() => !EstaCargando;

    /// <summary>
    /// Los atajos (Este mes, Mes anterior, Este año) pueden proponer un Desde anterior a la primera venta; se recorta a
    /// la fecha mínima en lugar de dejar que el servicio lo rechace (D-32).
    /// </summary>
    private DateTime LimitarAlMinimo(DateTime fecha) => fecha < FechaMinima ? FechaMinima : fecha;

    private async Task CargarFechaMinimaAsync()
    {
        var idActor = IdActor;

        await _carga.EjecutarOperacionAsync(
            token => Task.Run(() => _reporteService.ObtenerFechaMinimaAsync(idActor, token), token),
            minima =>
            {
                var fechaMinima = minima.ToDateTime(TimeOnly.MinValue);

                // Primero el Desde y después el límite del calendario: un Desde fuera de su rango no debe llegar al DatePicker.
                // Un Desde por defecto (día 1 del mes) anterior a la primera venta se corre a ella; si no, la primera
                // consulta del tablero sería rechazada.
                if (Desde is { } desde && desde < fechaMinima)
                {
                    Desde = fechaMinima;
                }

                FechaMinima = fechaMinima;
            },
            InformarError);
    }

    private async Task CargarListasAsync()
    {
        var idActor = IdActor;

        await _carga.EjecutarOperacionAsync(
            token => Task.Run(
                async () =>
                {
                    // Secuencial a propósito: las tres consultas comparten el DbContext de la pantalla.
                    var categorias = await _inventarioService.ListarCategoriasAsync(token);
                    var marcas = await _inventarioService.ListarMarcasAsync(token);
                    var usuarios = await _usuarioService.ListarUsuariosAsync(idActor, token);
                    return (Categorias: categorias, Marcas: marcas, Usuarios: usuarios);
                },
                token),
            listas =>
            {
                Reemplazar(Categorias, TextoTodas, listas.Categorias.Select(c => new OpcionFiltro(c.IdCategoria, c.NombreCategoria)));
                Reemplazar(Marcas, TextoTodas, listas.Marcas.Select(m => new OpcionFiltro(m.IdMarca, m.NombreMarca)));

                // El Administrador no opera ventas: no es un cajero que se pueda filtrar.
                Reemplazar(
                    Cajeros,
                    TextoTodos,
                    listas.Usuarios.Where(u => u.Rol != RolUsuarioEnum.Administrador).Select(u => new OpcionFiltro(u.IdUsuario, u.NombreCompleto)));

                CategoriaSeleccionada = Categorias[0];
                MarcaSeleccionada = Marcas[0];
                CajeroSeleccionado = Cajeros[0];
                _listasCargadas = true;
            },
            InformarError);

        // Si las listas fallaron igual se sigue: los combos quedan con "Todas" y los reportes funcionan sin esos filtros.
        _listasCargadas = true;
    }

    private async Task ConsultarReporteAsync(TipoReporteEnum tipo)
    {
        MensajeError = null;

        Func<CancellationToken, Task<ResultadoReporteDto>> consulta;
        try
        {
            consulta = CrearConsulta(tipo);
        }
        catch (InvalidOperationException ex)
        {
            MensajeError = ex.Message;
            EstaCargando = _carga.EnCurso;
            return;
        }

        EstaCargando = true;

        await _carga.EjecutarAsync(
            token => Task.Run(() => consulta(token), token),
            resultado =>
            {
                _resultados[tipo] = resultado;

                // El resultado puede llegar con otro reporte a la vista: solo se redibuja lo que se está mirando.
                ActualizarPresentacion();
            },
            InformarError);

        // Con una consulta más nueva en curso (otro reporte, otro clic) la pantalla sigue ocupada.
        EstaCargando = _carga.EnCurso;
    }

    private async Task ConsultarSiHaceFaltaAsync(TipoReporteEnum tipo)
    {
        if (!_resultados.ContainsKey(tipo))
        {
            await ConsultarReporteAsync(tipo);
        }
    }

    /// <summary>
    /// Arma la llamada al servicio con una copia de los filtros de este momento: lo que el Gerente cambie mientras se
    /// consulta no altera la consulta en curso. Se ejecuta en el hilo de UI; la consulta corre luego fuera de él.
    /// </summary>
    private Func<CancellationToken, Task<ResultadoReporteDto>> CrearConsulta(TipoReporteEnum tipo)
    {
        var idActor = IdActor;

        switch (tipo)
        {
            case TipoReporteEnum.Tablero:
                {
                    var (desde, hasta) = LeerPeriodo();
                    var filtro = new FiltroPeriodoDto { Desde = desde, Hasta = hasta };
                    return async token => await _reporteService.ObtenerTableroAsync(idActor, filtro, token);
                }

            case TipoReporteEnum.VentasPorPeriodo:
                {
                    var (desde, hasta) = LeerPeriodo();
                    var filtro = new FiltroVentasPorPeriodoDto
                    {
                        Desde = desde,
                        Hasta = hasta,
                        Agrupacion = Agrupacion,
                        IdUsuario = IdUsuario
                    };
                    return async token => await _reporteService.ObtenerVentasPorPeriodoAsync(idActor, filtro, token);
                }

            case TipoReporteEnum.Ranking:
                {
                    var (desde, hasta) = LeerPeriodo();
                    var filtro = new FiltroRankingDto
                    {
                        Desde = desde,
                        Hasta = hasta,
                        IdCategoria = IdCategoria,
                        IdMarca = IdMarca,
                        Criterio = Criterio,
                        Cantidad = Cantidad
                    };
                    return async token => await _reporteService.ObtenerRankingAsync(idActor, filtro, token);
                }

            case TipoReporteEnum.Rentabilidad:
                {
                    var (desde, hasta) = LeerPeriodo();
                    var filtro = new FiltroRentabilidadDto
                    {
                        Desde = desde,
                        Hasta = hasta,
                        IdCategoria = IdCategoria,
                        IdMarca = IdMarca,
                        AgruparPor = AgrupacionRentabilidad
                    };
                    return async token => await _reporteService.ObtenerRentabilidadAsync(idActor, filtro, token);
                }

            case TipoReporteEnum.StockCritico:
                {
                    var filtro = new FiltroInventarioDto { IdCategoria = IdCategoria, IdMarca = IdMarca };
                    return async token => await _reporteService.ObtenerStockCriticoAsync(idActor, filtro, token);
                }

            case TipoReporteEnum.SinMovimiento:
                {
                    var filtro = new FiltroSinMovimientoDto { IdCategoria = IdCategoria, IdMarca = IdMarca, Dias = Dias };
                    return async token => await _reporteService.ObtenerSinMovimientoAsync(idActor, filtro, token);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Reporte no soportado.");
        }
    }

    private (DateOnly Desde, DateOnly Hasta) LeerPeriodo()
    {
        if (Desde is null || Hasta is null)
        {
            throw new InvalidOperationException("Indique las fechas Desde y Hasta del período.");
        }

        return (DateOnly.FromDateTime(Desde.Value), DateOnly.FromDateTime(Hasta.Value));
    }

    // ---------------------------------------------------------------- Exportación

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    public Task ExportarExcelAsync() => ExportarAsync(FormatoExportacionEnum.Excel);

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    public Task ExportarPdfAsync() => ExportarAsync(FormatoExportacionEnum.Pdf);

    private bool PuedeExportar() => PuedeExportarResultado && !EstaCargando && !EstaExportando;

    /// <summary>
    /// Exporta el último resultado consultado del reporte visible, que es lo que el Gerente está viendo. Si cambió los
    /// filtros pero no volvió a consultar, el archivo conserva los filtros del resultado y no los nuevos: lo exportado
    /// siempre coincide con la pantalla (RF-26). Un fallo de escritura se reintenta con el MISMO resultado, sin consultar
    /// la base otra vez.
    /// </summary>
    private async Task ExportarAsync(FormatoExportacionEnum formato)
    {
        var resultado = ResultadoActual;
        if (resultado is null || EstaVacio)
        {
            return;
        }

        var ruta = _dialogService.ElegirArchivoDestino(NombreSugerido(ReporteSeleccionado, resultado), formato);
        if (ruta is null)
        {
            return;
        }

        var idActor = IdActor;
        EstaExportando = true;
        MensajeError = null;

        try
        {
            while (true)
            {
                var archivoAbierto = false;

                try
                {
                    // Ley 4: crear el archivo y escribirlo no corre en el hilo de UI; la generación del documento ya corre fuera de él.
                    await using var destino = await Task.Run(() => _dialogService.AbrirArchivoDestino(ruta));
                    archivoAbierto = true;
                    await _exportacionService.ExportarAsync(idActor, resultado, formato, destino);

                    _dialogService.MostrarExito($"Reporte guardado en {ruta}");
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "No se pudo guardar el reporte en {Ruta}: {Mensaje}", ruta, ex.Message);
                    BorrarArchivoParcial(ruta, archivoAbierto);

                    var reintentar = _dialogService.ConfirmarReintento(
                        $"No se pudo guardar el archivo: {ex.Message}. Cerralo si está abierto en otra aplicación. ¿Reintentar?");
                    if (!reintentar)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    BorrarArchivoParcial(ruta, archivoAbierto);
                    InformarError(ex);
                    return;
                }
            }
        }
        finally
        {
            EstaExportando = false;
        }
    }

    /// <summary>
    /// Si el fallo ocurrió DESPUÉS de abrir el archivo (al generar o al escribir), queda un archivo a medias: se borra
    /// antes de ofrecer el reintento. <c>FileMode.Create</c> ya lo había truncado, así que no se pierde nada que no se
    /// hubiera perdido. Si falló al ABRIRLO (por ejemplo, está abierto en Excel), el archivo existente no es nuestro y no
    /// se toca.
    /// </summary>
    private void BorrarArchivoParcial(string ruta, bool archivoAbierto)
    {
        if (archivoAbierto)
        {
            _dialogService.EliminarArchivoParcial(ruta);
        }
    }

    private string NombreSugerido(TipoReporteEnum tipo, ResultadoReporteDto resultado)
    {
        // Las fechas salen del resultado y no de los filtros de la pantalla, que pudieron cambiar después de consultar.
        FiltroPeriodoDto? periodo = resultado switch
        {
            TableroDto r => r.Filtro,
            VentasPorPeriodoDto r => r.Filtro,
            RankingArticulosDto r => r.Filtro,
            RentabilidadDto r => r.Filtro,
            _ => null
        };

        return periodo is null
            ? $"Reporte-{tipo}-{Hoy:yyyyMMdd}"
            : $"Reporte-{tipo}-{periodo.Desde:yyyyMMdd}-{periodo.Hasta:yyyyMMdd}";
    }

    // ---------------------------------------------------------------- Errores

    private void InformarError(Exception ex)
    {
        switch (ex)
        {
            case ValidationException validacion:
                MensajeError = validacion.Errors.Any()
                    ? string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage).Distinct())
                    : validacion.Message;
                break;

            case OperacionNoAutorizadaException:
                MensajeError = ex.Message;
                break;

            default:
                _logger.LogError(ex, "Error en el tablero gerencial: {Mensaje}", ex.Message);
                MensajeError = MensajeErrorInesperado;
                break;
        }
    }

    // ---------------------------------------------------------------- Presentación

    /// <summary>
    /// Vuelca el último resultado del reporte visible en las propiedades que enlaza la vista (tarjetas, gráficos,
    /// grillas y totales). Sin resultado, deja todo vacío.
    /// </summary>
    private void ActualizarPresentacion()
    {
        var tipo = ReporteSeleccionado;
        var resultado = _resultados.GetValueOrDefault(tipo);

        ResultadoActual = resultado;
        SinConsultar = resultado is null;

        EstaVacio = resultado switch
        {
            VentasPorPeriodoDto r => r.Puntos.Count == 0,
            RankingArticulosDto r => r.MasVendidos.Count == 0 && r.MenosVendidos.Count == 0,
            RentabilidadDto r => r.Filas.Count == 0,
            StockCriticoDto r => r.Filas.Count == 0,
            SinMovimientoDto r => r.Filas.Count == 0,
            _ => false
        };

        MensajeVacio = tipo switch
        {
            TipoReporteEnum.StockCritico => "No hay artículos bajo el stock mínimo con los filtros elegidos",
            TipoReporteEnum.SinMovimiento => "No hay artículos sin movimiento con los filtros elegidos",
            _ => "No hay datos para el período elegido"
        };

        var conDatos = resultado is not null && !EstaVacio;
        MostrarTablero = tipo == TipoReporteEnum.Tablero && resultado is not null;
        MostrarVentasPorPeriodo = tipo == TipoReporteEnum.VentasPorPeriodo && conDatos;
        MostrarRanking = tipo == TipoReporteEnum.Ranking && conDatos;
        MostrarRentabilidad = tipo == TipoReporteEnum.Rentabilidad && conDatos;
        MostrarStockCritico = tipo == TipoReporteEnum.StockCritico && conDatos;
        MostrarSinMovimiento = tipo == TipoReporteEnum.SinMovimiento && conDatos;
        MostrarGraficoReporte = MostrarRentabilidad;

        PresentarTablero(resultado as TableroDto);
        FilasVentasPorPeriodo = (resultado as VentasPorPeriodoDto)?.Puntos ?? [];
        MasVendidos = (resultado as RankingArticulosDto)?.MasVendidos ?? [];
        MenosVendidos = (resultado as RankingArticulosDto)?.MenosVendidos ?? [];
        FilasRentabilidad = (resultado as RentabilidadDto)?.Filas ?? [];
        TotalesVentas = resultado as VentasPorPeriodoDto;
        TotalRentabilidad = (resultado as RentabilidadDto)?.Total;
        FilasStockCritico = (resultado as StockCriticoDto)?.Filas ?? [];
        FilasSinMovimiento = (resultado as SinMovimientoDto)?.Filas ?? [];

        // Gráfico, título y totales del reporte visible: siempre salen de SU resultado guardado y se asignan de una vez
        // (sin pasar por un gráfico vacío), también al volver a una pestaña ya consultada.
        var (grafico, titulo, resumen) = ConstruirGraficoYResumen(resultado);
        GraficoReporte = grafico;
        TituloGraficoReporte = titulo;
        Resumen = resumen;

        OnPropertyChanged(nameof(PuedeExportarResultado));
        OnPropertyChanged(nameof(TooltipExportar));
        ExportarExcelCommand.NotifyCanExecuteChanged();
        ExportarPdfCommand.NotifyCanExecuteChanged();
    }

    private void PresentarTablero(TableroDto? tablero)
    {
        if (tablero is null)
        {
            GraficoEvolucion = GraficoPantalla.Vacio;
            GraficoMargenCategoria = GraficoPantalla.Vacio;
            return;
        }

        TotalVendidoTexto = FormatoReporte.Moneda(tablero.TotalVendido.Valor);
        TotalVendidoVariacion = FormatoReporte.Variacion(tablero.TotalVendido.VariacionPorcentual);

        CantidadVentasTexto = FormatoReporte.Entero((long)tablero.CantidadVentas.Valor);
        CantidadVentasVariacion = FormatoReporte.Variacion(tablero.CantidadVentas.VariacionPorcentual);

        MargenBrutoTexto = FormatoReporte.Moneda(tablero.MargenBruto.Valor);
        MargenBrutoVariacion = FormatoReporte.Variacion(tablero.MargenBruto.VariacionPorcentual);

        var desdeAnterior = tablero.DesdeAnterior.ToString("dd/MM/yyyy", FormatoReporte.Cultura);
        var hastaAnterior = tablero.HastaAnterior.ToString("dd/MM/yyyy", FormatoReporte.Cultura);
        TextoComparacion = $"Comparado con el período del {desdeAnterior} al {hastaAnterior}";

        ValorizacionTexto = FormatoReporte.Moneda(tablero.ValorizacionInventarioCosto);
        BajoMinimoTexto = $"{FormatoReporte.Entero(tablero.ArticulosBajoStockMinimo)} bajo mínimo";
        HayBajoMinimo = tablero.ArticulosBajoStockMinimo > 0;

        GraficoEvolucion = ConstructorGraficos.Lineas(
            "Total vendido",
            tablero.EvolucionVentas.Select(p => FormatoReporte.EtiquetaPeriodo(p.InicioPeriodo, tablero.AgrupacionEvolucion)).ToList(),
            tablero.EvolucionVentas.Select(p => p.TotalVendido).ToList());

        GraficoMargenCategoria = ConstructorGraficos.BarrasHorizontales(
            "Margen bruto",
            tablero.MargenPorCategoria.Select(f => f.Grupo).ToList(),
            tablero.MargenPorCategoria.Select(f => f.MargenBruto).ToList(),
            ConstructorGraficos.MaximoBarrasTablero);
        TituloMargenCategoria = "Margen bruto por categoría"
            + SufijoPrimeros(tablero.MargenPorCategoria.Count, ConstructorGraficos.MaximoBarrasTablero, "primeras");
    }

    /// <summary>
    /// Arma el gráfico del panel derecho, su título y la franja de totales para el resultado recibido. El tablero y los
    /// reportes sin gráfico (ventas por período, ranking, stock crítico y sin movimiento). Los totales de ventas y rentabilidad no van acá: tienen su propia fila alineada devuelven un gráfico vacío.
    /// </summary>
    private static (GraficoPantalla Grafico, string Titulo, IReadOnlyList<DatoResumen> Resumen) ConstruirGraficoYResumen(
        ResultadoReporteDto? resultado)
    {
        switch (resultado)
        {
            case RentabilidadDto rentabilidad:
                var agrupacion = rentabilidad.Filtro.AgruparPor;
                return (
                    ConstructorGraficos.BarrasHorizontales(
                        "Margen bruto",
                        rentabilidad.Filas.Select(f => f.Grupo).ToList(),
                        rentabilidad.Filas.Select(f => f.MargenBruto).ToList(),
                        ConstructorGraficos.MaximoBarrasReporte,
                        ConstructorGraficos.LargoEtiquetaReporte),
                    $"Margen bruto por {EnumATextoConverter.ATexto(agrupacion).ToLower(FormatoReporte.Cultura)}"
                        + SufijoPrimeros(
                            rentabilidad.Filas.Count,
                            ConstructorGraficos.MaximoBarrasReporte,
                            agrupacion == AgrupacionRentabilidadEnum.Articulo ? "primeros" : "primeras"),
                    []);

            case StockCriticoDto stock:
                return (
                    GraficoPantalla.Vacio,
                    string.Empty,
                    [
                        new DatoResumen("Artículos considerados", FormatoReporte.Entero(stock.ArticulosConsiderados)),
                        new DatoResumen("Valorización a costo", FormatoReporte.Moneda(stock.ValorizacionCosto)),
                        new DatoResumen("Valorización a venta", FormatoReporte.Moneda(stock.ValorizacionVenta))
                    ]);

            case SinMovimientoDto sinMovimiento:
                return (
                    GraficoPantalla.Vacio,
                    string.Empty,
                    [
                        new DatoResumen("Capital inmovilizado", FormatoReporte.Moneda(sinMovimiento.CapitalInmovilizadoTotal)),
                        new DatoResumen("Sin ventas desde", sinMovimiento.SinVentasDesde.ToString("dd/MM/yyyy", FormatoReporte.Cultura))
                    ]);

            default:
                return (GraficoPantalla.Vacio, string.Empty, []);
        }
    }

    /// <summary>Sufijo del título cuando el gráfico muestra menos elementos que la grilla: " (primeros 8)".</summary>
    private static string SufijoPrimeros(int cantidad, int maximo, string palabra)
    {
        return cantidad > maximo ? $" ({palabra} {maximo})" : string.Empty;
    }

    private static void Reemplazar(ObservableCollection<OpcionFiltro> destino, string textoTodas, IEnumerable<OpcionFiltro> opciones)
    {
        destino.Clear();
        destino.Add(new OpcionFiltro(null, textoTodas));
        foreach (var opcion in opciones)
        {
            destino.Add(opcion);
        }
    }
}
