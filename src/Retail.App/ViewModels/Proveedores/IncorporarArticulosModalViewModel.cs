using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el diálogo modal de confirmación y curaduría de incorporación de artículos a la tienda (Etapa 2.2, RF-05).
/// Permite definir un margen de ganancia global con aplicación masiva, ajustar márgenes individuales por producto con
/// cálculo reactivo del precio final de venta, y clasificar cada artículo (Track 6.6): con "Sugerir clasificación"
/// activado, el motor propone categoría y marca por ítem, en tres pasos (traducir valores del proveedor, términos
/// nuevos y revisión de artículos, P6-3); apagado, una categoría y una marca valen para todo el lote.
/// </summary>
public partial class IncorporarArticulosModalViewModel : ObservableObject, IDisposable
{
    // Opciones que encabezan los combos de la grilla, antes de las categorías y marcas del catálogo (M-70).
    private static readonly OpcionDiccionario[] OpcionesFijasDeCategoria = [OpcionDiccionario.SinClasificar];
    private static readonly OpcionDiccionario[] OpcionesFijasDeMarca = [OpcionDiccionario.SinClasificar, OpcionDiccionario.SinMarca];

    private readonly IClasificacionService _clasificacionService;
    private readonly ITerminoClasificacionService _terminoService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<IncorporarArticulosModalViewModel> _logger;

    // EF Core no admite dos operaciones simultáneas sobre el DbContext de la pantalla (H-19).
    private readonly SemaphoreSlim _accesoADatos = new(1, 1);
    private CancellationTokenSource _cancelacion = new();
    private SugerenciasClasificacionDto? _ultimaRespuesta;
    private int _idProveedor;
    private bool _inicializando;

    [ObservableProperty]
    private decimal _porcentajeGananciaGlobal = 40m;

    public decimal PorcentajeGanancia
    {
        get => PorcentajeGananciaGlobal;
        set => PorcentajeGananciaGlobal = value;
    }

    /// <summary>
    /// Categoría para todo el lote con el interruptor apagado. Usa las mismas opciones que la grilla (M-70): empieza en
    /// "Sin clasificar" y se puede volver a ella. Nulo vale lo mismo que "Sin clasificar".
    /// </summary>
    [ObservableProperty]
    private OpcionDiccionario? _categoriaDelLote = OpcionDiccionario.SinClasificar;

    /// <summary>
    /// Marca para todo el lote con el interruptor apagado: "Sin clasificar" (sin decidir), "Sin marca" (confirmada para
    /// todos los artículos, M-7) o una marca.
    /// </summary>
    [ObservableProperty]
    private OpcionDiccionario? _marcaDelLote = OpcionDiccionario.SinClasificar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public ObservableCollection<ItemIncorporarArticuloViewModel> Items { get; } = new();

    public ObservableCollection<CategoriaDto> CategoriasDisponibles { get; } = new();

    public ObservableCollection<MarcaDto> MarcasDisponibles { get; } = new();

    /// <summary>
    /// Opciones de los combos de la grilla con sugerencias: la primera es "Sin clasificar", falta de decisión (M-70).
    /// Se derivan de <see cref="CategoriasDisponibles"/> y se mantienen al día cuando se crea una categoría.
    /// </summary>
    public ObservableCollection<OpcionDiccionario> OpcionesCategoria { get; } = new(OpcionesFijasDeCategoria);

    /// <summary>
    /// Opciones de marca: "Sin clasificar" (sin decidir) y "Sin marca", la confirmación de que el producto no tiene
    /// marca (M-7); después, las marcas.
    /// </summary>
    public ObservableCollection<OpcionDiccionario> OpcionesMarca { get; } = new(OpcionesFijasDeMarca);

    public int TotalArticulos => Items.Count;

    public bool DialogResult { get; private set; }

    // ------------------------------------------------------------------ //
    // Sugerencias, pasos y permisos (Track 6.6)                          //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Interruptor "Sugerir clasificación" (M-11, P6-5): activado por defecto. Apagado, el modal funciona como antes de
    /// la Fase 6: una categoría y una marca para todo el lote, sin sugerencias.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarClasificacionDelLote))]
    [NotifyPropertyChangedFor(nameof(MostrarGrillaClasica))]
    [NotifyPropertyChangedFor(nameof(MostrarGrillaConSugerencias))]
    [NotifyPropertyChangedFor(nameof(MostrarEncabezadoDePasos))]
    [NotifyPropertyChangedFor(nameof(MostrarPasoTraducirValores))]
    [NotifyPropertyChangedFor(nameof(MostrarPasoTerminosNuevos))]
    [NotifyPropertyChangedFor(nameof(ArticulosSinCategoria))]
    [NotifyPropertyChangedFor(nameof(AvisoSinCategoria))]
    [NotifyPropertyChangedFor(nameof(HayArticulosSinCategoria))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonSiguiente))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonConfirmar))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonAnterior))]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(AceptarCommand))]
    private bool _sugerirClasificacion = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarGrillaConSugerencias))]
    [NotifyPropertyChangedFor(nameof(MostrarPasoTraducirValores))]
    [NotifyPropertyChangedFor(nameof(MostrarPasoTerminosNuevos))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(AceptarCommand))]
    private bool _cargandoSugerencias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarGrillaConSugerencias))]
    [NotifyPropertyChangedFor(nameof(MostrarPasoTraducirValores))]
    [NotifyPropertyChangedFor(nameof(MostrarPasoTerminosNuevos))]
    [NotifyPropertyChangedFor(nameof(EnPasoTraducirValores))]
    [NotifyPropertyChangedFor(nameof(EnPasoTerminosNuevos))]
    [NotifyPropertyChangedFor(nameof(EnPasoRevisarArticulos))]
    [NotifyPropertyChangedFor(nameof(Pasos))]
    [NotifyPropertyChangedFor(nameof(TextoSiguiente))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonSiguiente))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonConfirmar))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonAnterior))]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(AceptarCommand))]
    private PasoIncorporacionEnum _pasoActual = PasoIncorporacionEnum.RevisarArticulos;

    /// <summary>
    /// El primer cálculo de sugerencias encontró valores del proveedor sin traducir: el paso 1 forma parte del recorrido.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarEncabezadoDePasos))]
    [NotifyPropertyChangedFor(nameof(Pasos))]
    [NotifyPropertyChangedFor(nameof(TextoSiguiente))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonAnterior))]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    private bool _tienePasoTraducirValores;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarEncabezadoDePasos))]
    [NotifyPropertyChangedFor(nameof(Pasos))]
    [NotifyPropertyChangedFor(nameof(TextoSiguiente))]
    [NotifyPropertyChangedFor(nameof(MostrarBotonAnterior))]
    [NotifyCanExecuteChangedFor(nameof(AnteriorCommand))]
    private bool _tienePasoTerminosNuevos;

    public TraducirValoresPasoViewModel PasoTraducirValores { get; }

    public TerminosNuevosPasoViewModel PasoTerminosNuevos { get; }

    /// <summary>
    /// Solo el Gerente y el Encargado escriben diccionarios (D-5, P6-6); el resto ve los pasos 1 y 2 en solo lectura.
    /// </summary>
    public bool PuedeAdministrarClasificacion => _session.EsGerente || _session.EsEncargado;

    public bool MostrarClasificacionDelLote => !SugerirClasificacion;

    public bool MostrarGrillaClasica => !SugerirClasificacion;

    public bool MostrarGrillaConSugerencias =>
        SugerirClasificacion && PasoActual == PasoIncorporacionEnum.RevisarArticulos && !CargandoSugerencias;

    public bool MostrarEncabezadoDePasos => SugerirClasificacion && (TienePasoTraducirValores || TienePasoTerminosNuevos);

    public bool MostrarPasoTraducirValores =>
        SugerirClasificacion && PasoActual == PasoIncorporacionEnum.TraducirValores && !CargandoSugerencias;

    public bool MostrarPasoTerminosNuevos =>
        SugerirClasificacion && PasoActual == PasoIncorporacionEnum.TerminosNuevos && !CargandoSugerencias;

    public bool EnPasoTraducirValores => PasoActual == PasoIncorporacionEnum.TraducirValores;

    public bool EnPasoTerminosNuevos => PasoActual == PasoIncorporacionEnum.TerminosNuevos;

    public bool EnPasoRevisarArticulos => PasoActual == PasoIncorporacionEnum.RevisarArticulos;

    /// <summary>
    /// Pasos del recorrido en orden, solo los que tienen contenido (P6-3): alimenta el indicador de pasos.
    /// </summary>
    public IReadOnlyList<PasoDelRecorrido> Pasos
    {
        get
        {
            var pasos = PasosConContenido();
            int actual = pasos.FindIndex(p => p.Paso == PasoActual);
            return pasos
                .Select((p, i) => new PasoDelRecorrido(i + 1, p.Titulo, i == actual, i < actual, i == pasos.Count - 1))
                .ToList();
        }
    }

    /// <summary>
    /// El botón del pie nombra adónde lleva ("Siguiente: revisar artículos").
    /// </summary>
    public string TextoSiguiente =>
        Pasos.SkipWhile(p => !p.EsActual).Skip(1).FirstOrDefault() is { } siguiente
            ? $"Siguiente: {siguiente.Titulo.ToLowerInvariant()}"
            : "Siguiente";

    // Pie de asistente (M-70): con sugerencias, los pasos previos a la revisión muestran "Siguiente" en lugar de
    // "Confirmar"; sin sugerencias no hay recorrido y "Confirmar" queda como antes.
    public bool MostrarBotonSiguiente => SugerirClasificacion && !EnPasoRevisarArticulos;

    public bool MostrarBotonConfirmar => !MostrarBotonSiguiente;

    public bool MostrarBotonAnterior => SugerirClasificacion && PasoAnterior() is not null;

    /// <summary>
    /// Artículos que se incorporarían sin categoría: se pueden incorporar así, pero se avisa antes de confirmar.
    /// </summary>
    public int ArticulosSinCategoria => SugerirClasificacion
        ? Items.Count(i => i.SinCategoria)
        : (CategoriaDelLote?.Id is null ? Items.Count : 0);

    public bool HayArticulosSinCategoria => ArticulosSinCategoria > 0;

    public string? AvisoSinCategoria => ArticulosSinCategoria switch
    {
        0 => null,
        1 => "1 artículo se incorporará sin categoría.",
        var cantidad => $"{cantidad} artículos se incorporarán sin categoría."
    };

    public string ResumenClasificacion
    {
        get
        {
            int sugeridas = Items.Count(i => i.Estado == EstadoSugerenciaItemEnum.Sugerida);
            int corregidas = Items.Count(i => i.Estado == EstadoSugerenciaItemEnum.Corregida);
            return $"{sugeridas} sugeridas · {corregidas} corregidas";
        }
    }

    public IncorporarArticulosModalViewModel(
        IClasificacionService clasificacionService,
        ITerminoClasificacionService terminoService,
        IEquivalenciaClasificacionService equivalenciaService,
        IRubroService rubroService,
        ICurrentUserSession session,
        ILogger<IncorporarArticulosModalViewModel> logger)
    {
        _clasificacionService = clasificacionService ?? throw new ArgumentNullException(nameof(clasificacionService));
        _terminoService = terminoService ?? throw new ArgumentNullException(nameof(terminoService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        PasoTraducirValores = new TraducirValoresPasoViewModel(
            equivalenciaService, session, logger, CategoriasDisponibles, MarcasDisponibles);
        PasoTerminosNuevos = new TerminosNuevosPasoViewModel(
            terminoService, rubroService, session, logger, CategoriasDisponibles);

        CategoriasDisponibles.CollectionChanged += (_, e) => SincronizarOpciones(
            OpcionesCategoria,
            OpcionesFijasDeCategoria,
            e,
            CategoriasDisponibles.Select(c => new OpcionDiccionario(c.IdCategoria, c.NombreCategoria)));
        MarcasDisponibles.CollectionChanged += (_, e) => SincronizarOpciones(
            OpcionesMarca,
            OpcionesFijasDeMarca,
            e,
            MarcasDisponibles.Select(m => new OpcionDiccionario(m.IdMarca, m.NombreMarca)));
    }

    public void Inicializar(
        IEnumerable<CategoriaDto> categorias,
        IEnumerable<MarcaDto> marcas,
        IEnumerable<CatalogoProveedorDto>? items = null)
    {
        // El ViewModel se reutiliza entre aperturas: se cancela y descarta todo lo de la anterior. Mientras se
        // reinicia, el interruptor no dispara el cálculo: lo pide quien abre el diálogo con IniciarAsync.
        _inicializando = true;
        _cancelacion.Cancel();
        _cancelacion.Dispose();
        _cancelacion = new CancellationTokenSource();
        _ultimaRespuesta = null;
        _idProveedor = 0;

        CategoriasDisponibles.Clear();
        foreach (var c in categorias)
        {
            CategoriasDisponibles.Add(c);
        }

        MarcasDisponibles.Clear();
        foreach (var m in marcas)
        {
            MarcasDisponibles.Add(m);
        }

        foreach (var anterior in Items)
        {
            anterior.PropertyChanged -= AlCambiarUnItem;
        }

        Items.Clear();
        if (items != null)
        {
            foreach (var item in items)
            {
                _idProveedor = item.IdProveedor;
                var fila = new ItemIncorporarArticuloViewModel(
                    item.Id,
                    item.CodigoProveedor,
                    item.CodigoBarras,
                    item.DescripcionProveedor,
                    item.CostoReposicion,
                    PorcentajeGananciaGlobal,
                    OpcionesCategoria,
                    OpcionesMarca);
                fila.PropertyChanged += AlCambiarUnItem;
                Items.Add(fila);
            }
        }

        CategoriaDelLote = OpcionDiccionario.SinClasificar;
        MarcaDelLote = OpcionDiccionario.SinClasificar;
        MensajeError = null;
        DialogResult = false;
        SugerirClasificacion = true;
        CargandoSugerencias = false;
        TienePasoTraducirValores = false;
        TienePasoTerminosNuevos = false;
        PasoActual = PasoIncorporacionEnum.RevisarArticulos;
        PasoTraducirValores.Cargar(_idProveedor, Array.Empty<ValorSinTraducirDto>(), PuedeAdministrarClasificacion);
        PasoTerminosNuevos.Bloques.Clear();
        OnPropertyChanged(nameof(TotalArticulos));
        NotificarResumen();
        _inicializando = false;
    }

    partial void OnSugerirClasificacionChanged(bool value)
    {
        if (value && !_inicializando && _ultimaRespuesta is null && Items.Count > 0 && !CargandoSugerencias)
        {
            _ = IniciarAsync();
        }
    }

    partial void OnCategoriaDelLoteChanged(OpcionDiccionario? value)
    {
        NotificarResumen();
    }

    /// <summary>
    /// Pide las sugerencias del lote en segundo plano (Ley 4) y arma el recorrido: el primer paso con contenido es el
    /// inicial; si no hay valores ni bloques, directo a la revisión. Los errores se informan en el modal y no impiden
    /// clasificar a mano.
    /// </summary>
    public async Task IniciarAsync()
    {
        if (!SugerirClasificacion || Items.Count == 0)
        {
            return;
        }

        if (!await SugerirAsync() || _ultimaRespuesta is null)
        {
            PasoActual = PasoIncorporacionEnum.RevisarArticulos;
            return;
        }

        TienePasoTraducirValores = _ultimaRespuesta.ValoresSinTraducir.Count > 0;
        TienePasoTerminosNuevos = _ultimaRespuesta.Bloques.Count > 0;
        PasoActual = TienePasoTraducirValores
            ? PasoIncorporacionEnum.TraducirValores
            : TienePasoTerminosNuevos
                ? PasoIncorporacionEnum.TerminosNuevos
                : PasoIncorporacionEnum.RevisarArticulos;
    }

    /// <summary>
    /// Cancela lo que siga en curso al cerrarse el diálogo, antes de que se libere el DbContext de la pantalla.
    /// </summary>
    public void Finalizar()
    {
        _cancelacion.Cancel();
    }

    [RelayCommand(CanExecute = nameof(NoEstaCargando))]
    private async Task SiguienteAsync()
    {
        var desde = PasoActual;
        if (desde == PasoIncorporacionEnum.RevisarArticulos)
        {
            return;
        }

        // Lo decidido cambió los diccionarios: se vuelve a pedir al motor en lugar de recalcular en la UI (P6-3).
        await SugerirAsync();

        PasoActual = desde == PasoIncorporacionEnum.TraducirValores && PasoTerminosNuevos.Bloques.Count > 0
            ? PasoIncorporacionEnum.TerminosNuevos
            : PasoIncorporacionEnum.RevisarArticulos;
    }

    /// <summary>
    /// Vuelve al paso anterior del recorrido (M-70). No pide sugerencias: cada decisión de los pasos 1 y 2 se guardó al
    /// tomarla, y el paso muestra lo que la última respuesta dejó pendiente.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeVolverAlPasoAnterior))]
    private void Anterior()
    {
        if (PasoAnterior() is { } anterior)
        {
            PasoActual = anterior;
        }
    }

    private bool NoEstaCargando() => !CargandoSugerencias;

    private bool PuedeVolverAlPasoAnterior() => !CargandoSugerencias && MostrarBotonAnterior;

    // Con sugerencias, solo se confirma desde la revisión de artículos: así la grilla se ve antes de incorporar (M-70).
    private bool PuedeConfirmar() => !CargandoSugerencias && MostrarBotonConfirmar;

    private PasoIncorporacionEnum? PasoAnterior()
    {
        var pasos = PasosConContenido();
        int actual = pasos.FindIndex(p => p.Paso == PasoActual);
        return actual > 0 ? pasos[actual - 1].Paso : null;
    }

    private List<(PasoIncorporacionEnum Paso, string Titulo)> PasosConContenido()
    {
        var pasos = new List<(PasoIncorporacionEnum Paso, string Titulo)>();
        if (TienePasoTraducirValores)
        {
            pasos.Add((PasoIncorporacionEnum.TraducirValores, "Asociar categorías y marcas"));
        }

        if (TienePasoTerminosNuevos)
        {
            pasos.Add((PasoIncorporacionEnum.TerminosNuevos, "Términos nuevos"));
        }

        pasos.Add((PasoIncorporacionEnum.RevisarArticulos, "Revisar artículos"));
        return pasos;
    }

    [RelayCommand]
    private void RestablecerSugerencias()
    {
        foreach (var item in Items)
        {
            item.AceptarSugerencia();
        }
    }

    [RelayCommand]
    public void AplicarGananciaGlobal()
    {
        if (PorcentajeGananciaGlobal < 0m)
        {
            MensajeError = "El porcentaje de ganancia no puede ser negativo.";
            return;
        }

        MensajeError = null;
        foreach (var item in Items)
        {
            item.PorcentajeGanancia = PorcentajeGananciaGlobal;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private void Aceptar(Window window)
    {
        if (PorcentajeGananciaGlobal < 0m)
        {
            MensajeError = "El porcentaje de ganancia no puede ser negativo.";
            return;
        }

        if (Items.Any(i => i.PorcentajeGanancia < 0m))
        {
            MensajeError = "Ningún artículo puede tener un porcentaje de ganancia negativo.";
            return;
        }

        MensajeError = null;
        DialogResult = true;
        window.DialogResult = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancelar(Window window)
    {
        DialogResult = false;
        window.DialogResult = false;
        window.Close();
    }

    public IncorporarCatalogoArticulosDto ObtenerDto()
    {
        var itemsDto = Items.Select(i => SugerirClasificacion ? ItemConSugerencias(i) : ItemDelLote(i)).ToList();

        return new IncorporarCatalogoArticulosDto
        {
            Items = itemsDto
        };
    }

    /// <summary>
    /// Después de incorporar, avisa si el sistema propone reglas nuevas para revisar (Track 6.6, paso 6.8). Es un aviso:
    /// un fallo de la consulta se registra y no afecta a la incorporación, que ya se hizo.
    /// </summary>
    public async Task<string?> ObtenerAvisoReglasPropuestasAsync()
    {
        try
        {
            // Espera a que termine un cálculo de sugerencias cancelado: ambos comparten el DbContext de la pantalla.
            await _accesoADatos.WaitAsync();
            IReadOnlyList<ParPropuestoDto> propuestas;
            try
            {
                propuestas = await Task.Run(() => _terminoService.ListarParesPropuestosAsync(null));
            }
            finally
            {
                _accesoADatos.Release();
            }

            // Los pares propuestos se revisan dentro de cada término (M-71): el aviso nombra dónde buscarlos.
            string terminos = string.Join(", ", propuestas.Select(p => p.Termino).Distinct(StringComparer.Ordinal));
            return propuestas.Count switch
            {
                0 => null,
                1 => $"Hay 1 par propuesto para revisar en Artículos → Clasificación → Términos: {terminos}.",
                var cantidad => $"Hay {cantidad} pares propuestos para revisar en Artículos → Clasificación → Términos: {terminos}."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron consultar las reglas propuestas tras la incorporación");
            return null;
        }
    }

    // La categoría y la marca elegidas para todo el modal se aplican a cada ítem, sin sugerencia (origen Manual).
    private ItemIncorporacionArticuloDto ItemDelLote(ItemIncorporarArticuloViewModel item)
    {
        return new ItemIncorporacionArticuloDto
        {
            IdCatalogo = item.IdCatalogo,
            PorcentajeGanancia = item.PorcentajeGanancia,
            IdCategoria = CategoriaDelLote?.Id,
            IdMarca = MarcaDelLote?.Id,
            SinMarcaConfirmada = MarcaDelLote == OpcionDiccionario.SinMarca
        };
    }

    // Lleva lo confirmado y lo que sugirió el motor; el servidor deriva el origen del artículo (M-34).
    private static ItemIncorporacionArticuloDto ItemConSugerencias(ItemIncorporarArticuloViewModel item)
    {
        return new ItemIncorporacionArticuloDto
        {
            IdCatalogo = item.IdCatalogo,
            PorcentajeGanancia = item.PorcentajeGanancia,
            IdCategoria = item.CategoriaSeleccionada?.Id,
            IdCategoriaSugerida = item.IdCategoriaSugerida,
            IdMarca = item.MarcaSeleccionada?.Id,
            SinMarcaConfirmada = item.SinMarca,
            IdMarcaSugerida = item.IdMarcaSugerida
        };
    }

    private async Task<bool> SugerirAsync()
    {
        if (_idProveedor == 0 || Items.Count == 0)
        {
            return false;
        }

        CargandoSugerencias = true;
        MensajeError = null;
        var token = _cancelacion.Token;

        try
        {
            var solicitud = new SugerirClasificacionDto
            {
                IdProveedor = _idProveedor,
                IdsCatalogo = Items.Select(i => i.IdCatalogo).ToList()
            };

            SugerenciasClasificacionDto respuesta;
            await _accesoADatos.WaitAsync(token);
            try
            {
                respuesta = await Task.Run(() => _clasificacionService.SugerirAsync(solicitud, token), token);
            }
            finally
            {
                _accesoADatos.Release();
            }

            await AplicarRespuestaAsync(respuesta);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Regla de negocio al pedir sugerencias: {Mensaje}", ex.Message);
            MensajeError = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular las sugerencias de clasificación");
            MensajeError = "No se pudieron calcular las sugerencias. Puede clasificar los artículos a mano.";
            return false;
        }
        finally
        {
            CargandoSugerencias = false;
        }
    }

    private async Task AplicarRespuestaAsync(SugerenciasClasificacionDto respuesta)
    {
        _ultimaRespuesta = respuesta;

        var porIdCatalogo = respuesta.Items.ToDictionary(s => s.IdCatalogo);
        foreach (var item in Items)
        {
            item.AplicarSugerencia(porIdCatalogo.GetValueOrDefault(item.IdCatalogo));
        }

        bool puedeAdministrar = PuedeAdministrarClasificacion;
        PasoTraducirValores.Cargar(_idProveedor, respuesta.ValoresSinTraducir, puedeAdministrar);
        await PasoTerminosNuevos.CargarAsync(respuesta.Bloques, puedeAdministrar);
        NotificarResumen();
    }

    private void AlCambiarUnItem(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItemIncorporarArticuloViewModel.Estado)
            or nameof(ItemIncorporarArticuloViewModel.CategoriaSeleccionada))
        {
            NotificarResumen();
        }
    }

    /// <summary>
    /// Lo llama el contenedor de DI al descartarse la pantalla que lo creó.
    /// </summary>
    public void Dispose()
    {
        _cancelacion.Cancel();
        _cancelacion.Dispose();
        _accesoADatos.Dispose();
        GC.SuppressFinalize(this);
    }

    // Una categoría o marca agregada se suma al final; al vaciar la lista (nueva apertura) quedan solo las opciones fijas.
    // No se reconstruye en cada cambio: reiniciar la lista de un combo borraría la selección de las filas.
    private static void SincronizarOpciones(
        ObservableCollection<OpcionDiccionario> opciones,
        IReadOnlyList<OpcionDiccionario> fijas,
        NotifyCollectionChangedEventArgs e,
        IEnumerable<OpcionDiccionario> todas)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            opciones.Clear();
            foreach (var fija in fijas)
            {
                opciones.Add(fija);
            }

            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            // Los elementos nuevos son los últimos de la lista de origen.
            foreach (var nueva in todas.TakeLast(e.NewItems?.Count ?? 0))
            {
                opciones.Add(nueva);
            }
        }
    }

    private void NotificarResumen()
    {
        OnPropertyChanged(nameof(ArticulosSinCategoria));
        OnPropertyChanged(nameof(HayArticulosSinCategoria));
        OnPropertyChanged(nameof(AvisoSinCategoria));
        OnPropertyChanged(nameof(ResumenClasificacion));
    }
}
