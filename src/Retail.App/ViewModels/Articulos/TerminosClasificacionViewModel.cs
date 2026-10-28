using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// Sección "Términos" del diálogo de clasificación (Track 6.6, pasos 6.3 y 6.8): la tabla de términos del motor
/// de catalogación y, para el término ambiguo seleccionado, dos pestañas: sus pares (segunda palabra → categoría o
/// "Ignorado") y los pares que los artículos confirmados proponen (M-71).
/// </summary>
public partial class TerminosClasificacionViewModel : SeccionClasificacionViewModelBase
{
    /// <summary>Elemento del filtro que representa "todas las categorías" (Id 0: no es una categoría real).</summary>
    private static readonly CategoriaDto TodasLasCategorias = new()
    {
        IdCategoria = 0,
        NombreCategoria = "Todas las categorías"
    };

    private readonly ITerminoClasificacionService _terminoService;
    private readonly ICategoriaService _categoriaService;

    // Mientras se aplica un resultado de la base, cambiar una selección no debe disparar otra carga.
    private bool _aplicandoResultado;

    public ObservableCollection<TerminoDto> Terminos { get; } = new();

    public ObservableCollection<ParDto> Pares { get; } = new();

    /// <summary>Opciones del filtro por categoría: "Todas" y luego las categorías del catálogo.</summary>
    public ObservableCollection<CategoriaDto> CategoriasFiltro { get; } = new();

    [ObservableProperty]
    private string _filtro = string.Empty;

    [ObservableProperty]
    private CategoriaDto? _categoriaFiltro;

    [ObservableProperty]
    private bool _soloAmbiguos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayTerminoAmbiguoSeleccionado))]
    [NotifyCanExecuteChangedFor(nameof(NuevoParCommand))]
    private TerminoDto? _terminoSeleccionado;

    /// <summary>
    /// Pestaña del panel de pares: falso muestra los pares del diccionario; verdadero, los propuestos (M-71). Solo se
    /// carga la pestaña visible: calcular las propuestas lee los artículos con el término.
    /// </summary>
    [ObservableProperty]
    private bool _mostrandoPropuestos;

    /// <summary>Solo los términos ambiguos tienen pares (P-3): para el resto el panel de pares no se muestra.</summary>
    public bool HayTerminoAmbiguoSeleccionado => TerminoSeleccionado is { EsAmbiguo: true };

    /// <summary>Pestaña "Pares propuestos" del término seleccionado (M-49, M-71).</summary>
    public ParesPropuestosViewModel Propuestos { get; }

    public TerminosClasificacionViewModel(
        ITerminoClasificacionService terminoService,
        ICategoriaService categoriaService,
        IClasificacionDialogService dialogService,
        ICurrentUserSession session,
        CargaSerializada carga,
        ILogger logger)
        : base(dialogService, session, carga, logger)
    {
        _terminoService = terminoService ?? throw new ArgumentNullException(nameof(terminoService));
        _categoriaService = categoriaService ?? throw new ArgumentNullException(nameof(categoriaService));
        _categoriaFiltro = TodasLasCategorias;
        Propuestos = new ParesPropuestosViewModel(terminoService, dialogService, session, carga, logger, CargarParesAsync);
    }

    /// <summary>Carga la sección: las categorías del filtro, los términos y los pares del término elegido.</summary>
    public Task InicializarAsync()
    {
        return CargarAsync(TimeSpan.Zero, recargarCategorias: true);
    }

    partial void OnFiltroChanged(string value)
    {
        _ = CargarAsync(EsperaFiltro, recargarCategorias: false);
    }

    partial void OnCategoriaFiltroChanged(CategoriaDto? value)
    {
        if (!_aplicandoResultado)
        {
            _ = CargarAsync(TimeSpan.Zero, recargarCategorias: false);
        }
    }

    partial void OnSoloAmbiguosChanged(bool value)
    {
        _ = CargarAsync(TimeSpan.Zero, recargarCategorias: false);
    }

    partial void OnTerminoSeleccionadoChanged(TerminoDto? value)
    {
        if (!_aplicandoResultado)
        {
            _ = CargarParesAsync();
        }
    }

    partial void OnMostrandoPropuestosChanged(bool value)
    {
        _ = CargarParesAsync();
    }

    // ---------------------------------------------------------------- Términos

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private async Task NuevoTerminoAsync()
    {
        MensajeError = null;

        var resultado = DialogService.AbrirFormularioTermino(null, CategoriasReales());
        if (resultado is null)
        {
            return;
        }

        await EscribirAsync(
            "registrar el término",
            (idActor, token) => _terminoService.RegistrarAsync(
                idActor,
                new RegistrarTerminoDto { Termino = resultado.Texto, IdCategoria = resultado.IdOpcion },
                token),
            () => CargarAsync(TimeSpan.Zero, recargarCategorias: false));
    }

    /// <summary>
    /// Cambia la categoría de un término. Si el término es ambiguo, tiene pares y se le asigna una categoría, esos pares
    /// se dan de baja (P-3): se avisa cuántos son y se pide confirmación antes de guardar.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private async Task CambiarCategoriaAsync(TerminoDto? termino)
    {
        if (termino is null)
        {
            return;
        }

        MensajeError = null;

        var resultado = DialogService.AbrirFormularioTermino(termino, CategoriasReales());
        if (resultado is null)
        {
            return;
        }

        if (termino.EsAmbiguo && resultado.IdOpcion is not null)
        {
            var pares = await LeerAsync(
                "consultar los pares del término",
                token => _terminoService.ListarParesAsync(termino.IdTermino, null, true, token));
            if (pares is null)
            {
                return;
            }

            if (pares.Count > 0 && !DialogService.Confirmar(
                    "Confirmar cambio de categoría",
                    $"El término '{termino.Termino}' tiene {pares.Count} par(es) de palabras. Al asignarle una categoría, " +
                    "esos pares se dan de baja.\n¿Desea continuar?"))
            {
                return;
            }
        }

        await EscribirAsync(
            "cambiar la categoría del término",
            (idActor, token) => _terminoService.RegistrarAsync(
                idActor,
                new RegistrarTerminoDto { Termino = termino.Termino, IdCategoria = resultado.IdOpcion },
                token),
            () => CargarAsync(TimeSpan.Zero, recargarCategorias: false));
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task BajaTerminoAsync(TerminoDto? termino)
    {
        if (termino is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"el término '{termino.Termino}'",
            (idActor, token) => _terminoService.BajaAsync(idActor, termino.IdTermino, token),
            () => CargarAsync(TimeSpan.Zero, recargarCategorias: false));
    }

    // ------------------------------------------------------------------- Pares

    private bool PuedeAgregarPar => PuedeEscribir && HayTerminoAmbiguoSeleccionado;

    [RelayCommand(CanExecute = nameof(PuedeAgregarPar))]
    private async Task NuevoParAsync()
    {
        var termino = TerminoSeleccionado;
        if (termino is null)
        {
            return;
        }

        MensajeError = null;

        var resultado = DialogService.AbrirFormularioPar(termino, null, CategoriasReales());
        if (resultado is not null)
        {
            await AsignarParAsync(termino.IdTermino, resultado);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private async Task EditarParAsync(ParDto? par)
    {
        var termino = TerminoSeleccionado;
        if (par is null || termino is null)
        {
            return;
        }

        MensajeError = null;

        var resultado = DialogService.AbrirFormularioPar(termino, par, CategoriasReales());
        if (resultado is not null)
        {
            await AsignarParAsync(termino.IdTermino, resultado);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEscribir))]
    private Task QuitarParAsync(ParDto? par)
    {
        if (par is null)
        {
            return Task.CompletedTask;
        }

        return DarDeBajaAsync(
            $"el par '{par.Termino} {par.SegundaPalabra}'",
            (idActor, token) => _terminoService.QuitarParAsync(
                idActor,
                new QuitarParDto { IdTermino = par.IdTermino, SegundaPalabra = par.SegundaPalabra },
                token),
            CargarParesAsync);
    }

    private Task AsignarParAsync(int idTermino, ResultadoFormularioDiccionario resultado)
    {
        return EscribirAsync(
            "guardar el par",
            (idActor, token) => _terminoService.AsignarParAsync(
                idActor,
                new AsignarParDto
                {
                    IdTermino = idTermino,
                    SegundaPalabra = resultado.Texto,
                    IdCategoria = resultado.IdOpcion
                },
                token),
            CargarParesAsync);
    }

    // ------------------------------------------------------------------ Cargas

    private List<CategoriaDto> CategoriasReales()
    {
        return CategoriasFiltro.Where(c => c.IdCategoria != TodasLasCategorias.IdCategoria).ToList();
    }

    /// <summary>
    /// Recarga los términos con los filtros vigentes (resueltos en SQL, Ley 8), conserva la selección por Id y trae
    /// los pares del término elegido si es ambiguo.
    /// </summary>
    private async Task CargarAsync(TimeSpan espera, bool recargarCategorias)
    {
        string? filtro = NormalizarFiltro(Filtro);
        int? idCategoria = CategoriaFiltro is { IdCategoria: > 0 } categoria ? categoria.IdCategoria : null;
        bool soloAmbiguos = SoloAmbiguos;
        bool propuestos = MostrandoPropuestos;
        int? idPreferido = TerminoSeleccionado?.IdTermino;

        IsBusy = true;
        MensajeError = null;

        await Carga.EjecutarAsync(
            async token =>
            {
                // Consultas secuenciales: nunca se solapan sobre el DbContext.
                IReadOnlyList<CategoriaDto>? categorias = null;
                if (recargarCategorias)
                {
                    categorias = await Task.Run(() => _categoriaService.ListarAsync(null, null, token), token);
                }

                var terminos = await Task.Run(
                    () => _terminoService.ListarAsync(filtro, idCategoria, soloAmbiguos, token),
                    token);
                var elegido = terminos.FirstOrDefault(t => t.IdTermino == idPreferido);

                var detalle = elegido is { EsAmbiguo: true }
                    ? await LeerDetalleAsync(elegido.IdTermino, propuestos, token)
                    : DetalleDelTermino.Vacio;

                return (Categorias: categorias, Terminos: terminos, IdElegido: elegido?.IdTermino, Detalle: detalle);
            },
            resultado => Aplicar(resultado.Categorias, resultado.Terminos, resultado.IdElegido, resultado.Detalle),
            ex => InformarError("cargar los términos", ex),
            espera);

        IsBusy = Carga.EnCurso;
    }

    private async Task CargarParesAsync()
    {
        var termino = TerminoSeleccionado;
        if (termino is not { EsAmbiguo: true })
        {
            EjecutarEnDispatcher(() => AplicarDetalle(DetalleDelTermino.Vacio));
            return;
        }

        bool propuestos = MostrandoPropuestos;
        IsBusy = true;
        MensajeError = null;
        Propuestos.MensajeError = null;

        await Carga.EjecutarAsync(
            token => LeerDetalleAsync(termino.IdTermino, propuestos, token),
            detalle => EjecutarEnDispatcher(() => AplicarDetalle(detalle)),
            ex => InformarError(propuestos ? "calcular los pares propuestos" : "cargar los pares del término", ex));

        IsBusy = Carga.EnCurso;
    }

    /// <summary>
    /// Lo que muestra el panel derecho para un término ambiguo: sus pares o sus propuestas, según la pestaña. La otra
    /// lista queda nula y no se toca.
    /// </summary>
    private sealed record DetalleDelTermino(IReadOnlyList<ParDto>? Pares, IReadOnlyList<ParPropuestoDto>? Propuestas)
    {
        public static DetalleDelTermino Vacio { get; } = new([], []);
    }

    private async Task<DetalleDelTermino> LeerDetalleAsync(int idTermino, bool propuestos, CancellationToken token)
    {
        if (propuestos)
        {
            var propuestas = await Task.Run(() => _terminoService.ListarParesPropuestosAsync(idTermino, token), token);
            return new DetalleDelTermino(null, propuestas);
        }

        var pares = await Task.Run(() => _terminoService.ListarParesAsync(idTermino, null, true, token), token);
        return new DetalleDelTermino(pares, null);
    }

    private void AplicarDetalle(DetalleDelTermino detalle)
    {
        if (detalle.Pares is not null)
        {
            Reemplazar(Pares, detalle.Pares);
        }

        if (detalle.Propuestas is not null)
        {
            Propuestos.Mostrar(CategoriasReales(), detalle.Propuestas);
        }
    }

    private void Aplicar(
        IReadOnlyList<CategoriaDto>? categorias,
        IReadOnlyList<TerminoDto> terminos,
        int? idElegido,
        DetalleDelTermino detalle)
    {
        EjecutarEnDispatcher(() =>
        {
            _aplicandoResultado = true;
            try
            {
                if (categorias is not null)
                {
                    int idFiltro = CategoriaFiltro?.IdCategoria ?? 0;
                    Reemplazar(CategoriasFiltro, [TodasLasCategorias, .. categorias]);
                    CategoriaFiltro = CategoriasFiltro.FirstOrDefault(c => c.IdCategoria == idFiltro) ?? TodasLasCategorias;
                }

                Reemplazar(Terminos, terminos);
                TerminoSeleccionado = idElegido.HasValue
                    ? Terminos.FirstOrDefault(t => t.IdTermino == idElegido.Value)
                    : null;
                AplicarDetalle(detalle);
            }
            finally
            {
                _aplicandoResultado = false;
            }
        });
    }
}
