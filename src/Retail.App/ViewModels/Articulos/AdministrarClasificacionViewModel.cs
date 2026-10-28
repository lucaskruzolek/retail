using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel del diálogo de administración de la clasificación comercial (Track 6.5, RF-04, D-26):
/// rubros a la izquierda y categorías del rubro seleccionado a la derecha, o bien las marcas con sus alias y prefijos
/// (M-68), y desde el Track 6.6 los términos con sus pares y pares propuestos (M-71), alternando las secciones con
/// botones de conmutación. Cada diccionario tiene su propio ViewModel hijo (P6-7).
/// </summary>
/// <remarks>
/// Todas las lecturas pasan por una única <see cref="CargaSerializada"/>: el diálogo comparte el DbContext
/// de la pantalla de artículos y EF Core no admite dos operaciones simultáneas sobre un mismo contexto (H-19);
/// los ViewModels hijos reciben esa misma instancia.
/// </remarks>
public partial class AdministrarClasificacionViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan EsperaFiltro = TimeSpan.FromMilliseconds(250);

    private readonly IRubroService _rubroService;
    private readonly ICategoriaService _categoriaService;
    private readonly IMarcaService _marcaService;
    private readonly IClasificacionDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<AdministrarClasificacionViewModel> _logger;
    private readonly CargaSerializada _carga;

    // Mientras se aplica un resultado de la base, cambiar RubroSeleccionado no debe disparar otra carga.
    private bool _aplicandoResultado;

    public ObservableCollection<RubroDto> Rubros { get; } = new();

    public ObservableCollection<CategoriaDto> Categorias { get; } = new();

    public ObservableCollection<MarcaDto> Marcas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayRubroSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(NuevaCategoriaCommand))]
    private RubroDto? _rubroSeleccionado;

    [ObservableProperty]
    private string _filtroRubro = string.Empty;

    [ObservableProperty]
    private string _filtroCategoria = string.Empty;

    [ObservableProperty]
    private string _filtroMarca = string.Empty;

    /// <summary>Marca cuyo detalle (alias y prefijos EAN) se muestra al lado de la lista (M-68).</summary>
    [ObservableProperty]
    private MarcaDto? _marcaSeleccionada;

    [ObservableProperty]
    private bool _mostrandoRubros = true;

    [ObservableProperty]
    private bool _mostrandoMarcas;

    [ObservableProperty]
    private bool _mostrandoTerminos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    // Mientras hay una carga en curso (incluida la espera previa del filtro) no se abre ningún formulario ni se
    // da de baja: el diálogo comparte el DbContext de la pantalla y dos operaciones simultáneas sobre él fallan
    // (SISTEMA_DE_PERSISTENCIA, ley 8).
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NuevoRubroCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditarRubroCommand))]
    [NotifyCanExecuteChangedFor(nameof(BajaRubroCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevaCategoriaCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditarCategoriaCommand))]
    [NotifyCanExecuteChangedFor(nameof(BajaCategoriaCommand))]
    [NotifyCanExecuteChangedFor(nameof(NuevaMarcaCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditarMarcaCommand))]
    [NotifyCanExecuteChangedFor(nameof(BajaMarcaCommand))]
    private bool _isBusy;

    /// <summary>Sección Términos: los pares y pares propuestos del término ambiguo seleccionado (Track 6.6, M-71).</summary>
    public TerminosClasificacionViewModel Terminos { get; }

    /// <summary>Alias y prefijos EAN de la marca seleccionada, dentro de la sección Marcas (Track 6.6, M-68).</summary>
    public DetalleMarcaViewModel DetalleMarca { get; }

    public bool HayRubroSeleccionado => RubroSeleccionado is not null;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>Indica si se guardó o dio de baja algún elemento: el llamador debe recargar sus combos.</summary>
    public bool HuboCambios { get; private set; }

    public AdministrarClasificacionViewModel(
        IRubroService rubroService,
        ICategoriaService categoriaService,
        IMarcaService marcaService,
        ITerminoClasificacionService terminoService,
        IClasificacionDialogService dialogService,
        ICurrentUserSession session,
        ILogger<AdministrarClasificacionViewModel>? logger = null)
    {
        _rubroService = rubroService ?? throw new ArgumentNullException(nameof(rubroService));
        _categoriaService = categoriaService ?? throw new ArgumentNullException(nameof(categoriaService));
        _marcaService = marcaService ?? throw new ArgumentNullException(nameof(marcaService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? NullLogger<AdministrarClasificacionViewModel>.Instance;
        _carga = new CargaSerializada(_logger);

        ArgumentNullException.ThrowIfNull(terminoService);
        Terminos = new TerminosClasificacionViewModel(terminoService, categoriaService, dialogService, session, _carga, _logger);
        DetalleMarca = new DetalleMarcaViewModel(marcaService, dialogService, session, _carga, _logger);
    }

    /// <summary>Carga inicial: rubros (selecciona el primero con sus categorías) y marcas.</summary>
    public async Task InicializarAsync()
    {
        await CargarRubrosYCategoriasAsync(TimeSpan.Zero);
        await CargarMarcasAsync(TimeSpan.Zero);
    }

    private enum Seccion
    {
        Rubros,
        Marcas,
        Terminos
    }

    // Las secciones son mutuamente excluyentes y siempre hay una visible. Las del Track 6.6 se recargan cada vez
    // que se muestran: lo que listan puede haber cambiado desde la última vez.
    partial void OnMostrandoRubrosChanged(bool value)
    {
        AlternarSeccion(Seccion.Rubros, value);
    }

    partial void OnMostrandoMarcasChanged(bool value)
    {
        if (AlternarSeccion(Seccion.Marcas, value))
        {
            _ = DetalleMarca.MostrarAsync(MarcaSeleccionada);
        }
    }

    partial void OnMostrandoTerminosChanged(bool value)
    {
        if (AlternarSeccion(Seccion.Terminos, value))
        {
            _ = Terminos.InicializarAsync();
        }
    }

    /// <summary>Devuelve true cuando <paramref name="seccion"/> acaba de pasar a mostrarse.</summary>
    private bool AlternarSeccion(Seccion seccion, bool value)
    {
        var estados = new (Seccion Seccion, bool Visible, Action<bool> Asignar)[]
        {
            (Seccion.Rubros, MostrandoRubros, v => MostrandoRubros = v),
            (Seccion.Marcas, MostrandoMarcas, v => MostrandoMarcas = v),
            (Seccion.Terminos, MostrandoTerminos, v => MostrandoTerminos = v)
        };

        if (value)
        {
            foreach (var otra in estados.Where(e => e.Seccion != seccion && e.Visible))
            {
                otra.Asignar(false);
            }

            return true;
        }

        if (!estados.Any(e => e.Visible))
        {
            estados[(int)seccion].Asignar(true);
        }

        return false;
    }

    partial void OnRubroSeleccionadoChanged(RubroDto? value)
    {
        if (!_aplicandoResultado)
        {
            _ = CargarRubrosYCategoriasAsync(TimeSpan.Zero);
        }
    }

    partial void OnFiltroRubroChanged(string value)
    {
        _ = CargarRubrosYCategoriasAsync(EsperaFiltro);
    }

    partial void OnFiltroCategoriaChanged(string value)
    {
        _ = CargarRubrosYCategoriasAsync(EsperaFiltro);
    }

    partial void OnFiltroMarcaChanged(string value)
    {
        _ = CargarMarcasAsync(EsperaFiltro);
    }

    // El detalle se carga solo con la sección visible: oculta, una carga de otra sección lo cancelaría (carga
    // reemplazable) y quedaría vacío. Al volver a mostrarse, OnMostrandoMarcasChanged lo recarga.
    partial void OnMarcaSeleccionadaChanged(MarcaDto? value)
    {
        if (MostrandoMarcas)
        {
            _ = DetalleMarca.MostrarAsync(value);
        }
    }

    // ------------------------------------------------------------------ Rubros

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private async Task NuevoRubroAsync()
    {
        MensajeError = null;
        if (_dialogService.AbrirFormularioRubro(null))
        {
            HuboCambios = true;
            await CargarRubrosYCategoriasAsync(TimeSpan.Zero);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private async Task EditarRubroAsync(RubroDto? rubro)
    {
        if (rubro is null)
        {
            return;
        }

        MensajeError = null;
        if (_dialogService.AbrirFormularioRubro(rubro))
        {
            HuboCambios = true;
            await CargarRubrosYCategoriasAsync(TimeSpan.Zero);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private Task BajaRubroAsync(RubroDto? rubro)
    {
        if (rubro is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"el rubro '{rubro.NombreRubro}'",
            (idActor, token) => _rubroService.BajaAsync(idActor, rubro.IdRubro, token),
            () => CargarRubrosYCategoriasAsync(TimeSpan.Zero));
    }

    // -------------------------------------------------------------- Categorías

    private bool PuedeOperar => !IsBusy;

    private bool PuedeCrearCategoria => RubroSeleccionado is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeCrearCategoria))]
    private async Task NuevaCategoriaAsync()
    {
        MensajeError = null;
        var rubros = await ObtenerRubrosParaFormularioAsync();
        if (rubros is null)
        {
            return;
        }

        if (_dialogService.AbrirFormularioCategoria(null, rubros, RubroSeleccionado?.IdRubro))
        {
            HuboCambios = true;

            // Una categoría nueva cambia el conteo de su rubro: se recargan ambas grillas.
            await CargarRubrosYCategoriasAsync(TimeSpan.Zero);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private async Task EditarCategoriaAsync(CategoriaDto? categoria)
    {
        if (categoria is null)
        {
            return;
        }

        MensajeError = null;
        var rubros = await ObtenerRubrosParaFormularioAsync();
        if (rubros is null)
        {
            return;
        }

        if (_dialogService.AbrirFormularioCategoria(categoria, rubros, categoria.IdRubro))
        {
            HuboCambios = true;
            await CargarRubrosYCategoriasAsync(TimeSpan.Zero);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private Task BajaCategoriaAsync(CategoriaDto? categoria)
    {
        if (categoria is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"la categoría '{categoria.NombreCategoria}'",
            (idActor, token) => _categoriaService.BajaAsync(idActor, categoria.IdCategoria, token),
            () => CargarRubrosYCategoriasAsync(TimeSpan.Zero));
    }

    // ------------------------------------------------------------------ Marcas

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private async Task NuevaMarcaAsync()
    {
        MensajeError = null;
        if (_dialogService.AbrirFormularioMarca(null))
        {
            HuboCambios = true;
            await CargarMarcasAsync(TimeSpan.Zero);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private async Task EditarMarcaAsync(MarcaDto? marca)
    {
        if (marca is null)
        {
            return;
        }

        MensajeError = null;
        if (_dialogService.AbrirFormularioMarca(marca))
        {
            HuboCambios = true;
            await CargarMarcasAsync(TimeSpan.Zero);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeOperar))]
    private Task BajaMarcaAsync(MarcaDto? marca)
    {
        if (marca is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"la marca '{marca.NombreMarca}'",
            (idActor, token) => _marcaService.BajaAsync(idActor, marca.IdMarca, token),
            () => CargarMarcasAsync(TimeSpan.Zero));
    }

    // ------------------------------------------------------------------ Cargas

    /// <summary>
    /// Recarga la sección de rubros: lista los rubros filtrados, conserva la selección por Id (o elige el primero)
    /// y trae las categorías de ese rubro con el filtro resuelto en SQL (D-26, Ley 8).
    /// </summary>
    private async Task CargarRubrosYCategoriasAsync(TimeSpan espera)
    {
        string? filtroRubro = NormalizarFiltro(FiltroRubro);
        string? filtroCategoria = NormalizarFiltro(FiltroCategoria);
        int? idPreferido = RubroSeleccionado?.IdRubro;

        IsBusy = true;
        MensajeError = null;

        await _carga.EjecutarAsync(
            async token =>
            {
                // Consultas secuenciales: nunca se solapan sobre el DbContext.
                var rubros = await Task.Run(() => _rubroService.ListarAsync(filtroRubro, token), token);
                var elegido = rubros.FirstOrDefault(r => r.IdRubro == idPreferido) ?? (rubros.Count > 0 ? rubros[0] : null);

                IReadOnlyList<CategoriaDto> categorias = [];
                if (elegido is not null)
                {
                    categorias = await Task.Run(
                        () => _categoriaService.ListarAsync(filtroCategoria, elegido.IdRubro, token),
                        token);
                }

                return (Rubros: rubros, IdElegido: elegido?.IdRubro, Categorias: categorias);
            },
            resultado => AplicarRubrosYCategorias(resultado.Rubros, resultado.IdElegido, resultado.Categorias),
            ex => InformarError("cargar los rubros y categorías", ex),
            espera);

        IsBusy = _carga.EnCurso;
    }

    /// <summary>
    /// Recarga la lista de marcas y conserva la selección por Id (o elige la primera), como la sección de rubros.
    /// </summary>
    private async Task CargarMarcasAsync(TimeSpan espera)
    {
        string? filtro = NormalizarFiltro(FiltroMarca);
        int? idPreferido = MarcaSeleccionada?.IdMarca;

        IsBusy = true;
        MensajeError = null;

        await _carga.EjecutarAsync(
            token => Task.Run(() => _marcaService.ListarAsync(filtro, token), token),
            marcas => EjecutarEnDispatcher(() =>
            {
                Reemplazar(Marcas, marcas);
                MarcaSeleccionada = Marcas.FirstOrDefault(m => m.IdMarca == idPreferido) ?? Marcas.FirstOrDefault();
            }),
            ex => InformarError("cargar las marcas", ex),
            espera);

        IsBusy = _carga.EnCurso;
    }

    private void AplicarRubrosYCategorias(
        IReadOnlyList<RubroDto> rubros,
        int? idElegido,
        IReadOnlyList<CategoriaDto> categorias)
    {
        EjecutarEnDispatcher(() =>
        {
            _aplicandoResultado = true;
            try
            {
                Reemplazar(Rubros, rubros);
                RubroSeleccionado = idElegido.HasValue
                    ? Rubros.FirstOrDefault(r => r.IdRubro == idElegido.Value)
                    : null;
                Reemplazar(Categorias, categorias);
            }
            finally
            {
                _aplicandoResultado = false;
            }
        });
    }

    /// <summary>Reemplaza el contenido solo si cambió, para no perder selección ni posición de scroll de la grilla.</summary>
    private static void Reemplazar<T>(ObservableCollection<T> destino, IReadOnlyList<T> nuevos)
    {
        if (destino.SequenceEqual(nuevos))
        {
            return;
        }

        destino.Clear();
        foreach (var item in nuevos)
        {
            destino.Add(item);
        }
    }

    /// <summary>
    /// Trae todos los rubros, sin filtro, para el combo del formulario de categorías: con un filtro activo en la
    /// grilla, la lista visible estaría incompleta.
    /// </summary>
    private async Task<IReadOnlyList<RubroDto>?> ObtenerRubrosParaFormularioAsync()
    {
        IReadOnlyList<RubroDto>? rubros = null;

        await _carga.EjecutarOperacionAsync(
            token => Task.Run(() => _rubroService.ListarAsync(null, token), token),
            resultado => rubros = resultado,
            ex => InformarError("obtener los rubros", ex));

        return rubros;
    }

    // -------------------------------------------------------------------- Baja

    private async Task DarDeBajaAsync(
        string descripcionElemento,
        Func<int, CancellationToken, Task> baja,
        Func<Task> recargar)
    {
        MensajeError = null;

        bool confirmado = _dialogService.Confirmar(
            "Confirmar baja",
            $"¿Está seguro de dar de baja {descripcionElemento}?\nEl registro será archivado lógicamente.");
        if (!confirmado)
        {
            return;
        }

        int idActor = _session.IdUsuario ?? 0;
        bool exito = false;

        IsBusy = true;
        try
        {
            await _carga.EjecutarOperacionAsync(
                async token =>
                {
                    await Task.Run(() => baja(idActor, token), token);
                    return true;
                },
                _ => exito = true,
                ex => InformarError("dar de baja el elemento", ex));

            if (exito)
            {
                HuboCambios = true;
                await recargar();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void InformarError(string accion, Exception ex)
    {
        if (ex is DomainException)
        {
            // Incluye ClasificacionEnUsoException (D-1) y OperacionNoAutorizadaException (D-5).
            _logger.LogWarning(ex, "Regla de negocio al {Accion}: {Mensaje}", accion, ex.Message);
            MensajeError = ex.Message;
            return;
        }

        _logger.LogError(ex, "Error inesperado al {Accion}: {Mensaje}", accion, ex.Message);
        MensajeError = $"Error inesperado al {accion}: {ex.Message}";
    }

    private static string? NormalizarFiltro(string filtro)
    {
        return string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();
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

    /// <summary>Cancela las cargas pendientes al cerrar el diálogo, antes de que se libere el DbContext.</summary>
    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}
