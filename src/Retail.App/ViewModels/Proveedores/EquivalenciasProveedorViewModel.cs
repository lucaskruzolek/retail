using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel del diálogo de equivalencias de clasificación de un proveedor (Track 6.6, Fase 6, P6-7): lista cómo se
/// traduce lo que el proveedor escribe (rubro y categoría, o marca) a las categorías y marcas del comercio, y permite
/// asignar un destino (o ignorar el valor) y quitar la equivalencia.
/// </summary>
/// <remarks>
/// Escribir equivalencias exige ser Gerente o Encargado (D-5); sin esa autoridad el diálogo es de solo lectura
/// (P6-6). Todo acceso a datos pasa por una única <see cref="CargaSerializada"/>: el diálogo comparte el DbContext
/// de la pantalla de proveedores (H-19).
/// </remarks>
public partial class EquivalenciasProveedorViewModel : ObservableObject, IDisposable
{
    private static readonly OpcionTipoEquivalencia TipoCategoria = new(TipoEquivalenciaEnum.Categoria, "Categoría");
    private static readonly OpcionTipoEquivalencia TipoMarca = new(TipoEquivalenciaEnum.Marca, "Marca");
    private static readonly OpcionDestinoEquivalencia OpcionIgnorar = new(null, "Ignorar (no traducir)");

    private readonly IEquivalenciaClasificacionService _equivalenciaService;
    private readonly ICategoriaService _categoriaService;
    private readonly IMarcaService _marcaService;
    private readonly IClasificacionDialogService _dialogService;
    private readonly ICurrentUserSession _session;
    private readonly ILogger<EquivalenciasProveedorViewModel> _logger;
    private readonly CargaSerializada _carga;

    private IReadOnlyList<CategoriaDto> _categorias = [];
    private IReadOnlyList<MarcaDto> _marcas = [];

    public ObservableCollection<EquivalenciaProveedorFila> Equivalencias { get; } = new();

    public ObservableCollection<OpcionDestinoEquivalencia> Destinos { get; } = new();

    public IReadOnlyList<OpcionTipoEquivalencia> TiposDisponibles { get; } =
        [TipoCategoria, TipoMarca];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    private ProveedorDto? _proveedor;

    [ObservableProperty]
    private EquivalenciaProveedorFila? _filaSeleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsTipoCategoria))]
    private OpcionTipoEquivalencia _tipoSeleccionado = TipoCategoria;

    [ObservableProperty]
    private string _textoRubro = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AsignarCommand))]
    private string _texto = string.Empty;

    [ObservableProperty]
    private OpcionDestinoEquivalencia? _destinoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AsignarCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuitarCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstado))]
    private string? _mensajeEstado;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public bool TieneMensajeEstado => !string.IsNullOrWhiteSpace(MensajeEstado);

    public bool EsTipoCategoria => TipoSeleccionado.Tipo == TipoEquivalenciaEnum.Categoria;

    /// <summary>Solo el Gerente y el Encargado escriben diccionarios de clasificación (D-5).</summary>
    public bool PuedeEditar => _session.EsGerente || _session.EsEncargado;

    public bool SoloLectura => !PuedeEditar;

    public string TituloVentana => Proveedor is null
        ? "Equivalencias del proveedor"
        : $"Equivalencias de {Proveedor.RazonSocial}";

    public EquivalenciasProveedorViewModel(
        IEquivalenciaClasificacionService equivalenciaService,
        ICategoriaService categoriaService,
        IMarcaService marcaService,
        IClasificacionDialogService dialogService,
        ICurrentUserSession session,
        ILogger<EquivalenciasProveedorViewModel>? logger = null)
    {
        _equivalenciaService = equivalenciaService ?? throw new ArgumentNullException(nameof(equivalenciaService));
        _categoriaService = categoriaService ?? throw new ArgumentNullException(nameof(categoriaService));
        _marcaService = marcaService ?? throw new ArgumentNullException(nameof(marcaService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? NullLogger<EquivalenciasProveedorViewModel>.Instance;
        _carga = new CargaSerializada(_logger);

        ReconstruirDestinos();
    }

    /// <summary>Indica a qué proveedor pertenecen las equivalencias; se invoca antes de mostrar el diálogo.</summary>
    public void Configurar(ProveedorDto proveedor)
    {
        Proveedor = proveedor ?? throw new ArgumentNullException(nameof(proveedor));
    }

    /// <summary>Carga inicial: categorías y marcas para el combo de destino, y las equivalencias del proveedor.</summary>
    public async Task InicializarAsync()
    {
        if (Proveedor is null)
        {
            return;
        }

        int idProveedor = Proveedor.IdProveedor;
        MensajeError = null;
        IsBusy = true;
        try
        {
            await _carga.EjecutarAsync(
                token => Task.Run(
                    async () =>
                    {
                        var categorias = await _categoriaService.ListarAsync(null, null, token);
                        var marcas = await _marcaService.ListarAsync(null, token);
                        var equivalencias = await _equivalenciaService.ListarAsync(idProveedor, null, token);
                        return (categorias, marcas, equivalencias);
                    },
                    token),
                resultado =>
                {
                    _categorias = resultado.categorias;
                    _marcas = resultado.marcas;
                    ReconstruirDestinos();
                    AplicarEquivalencias(resultado.equivalencias);
                },
                ex => InformarError("cargar las equivalencias", ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnTipoSeleccionadoChanged(OpcionTipoEquivalencia value)
    {
        if (value.Tipo == TipoEquivalenciaEnum.Marca)
        {
            TextoRubro = string.Empty;
        }

        ReconstruirDestinos();
    }

    // Seleccionar una fila la vuelca al formulario para cambiar su destino; el rubro y el texto identifican la
    // equivalencia, así que asignar sobre ellos reemplaza el destino en lugar de crear otra (M-31).
    partial void OnFilaSeleccionadaChanged(EquivalenciaProveedorFila? value)
    {
        if (value is null)
        {
            return;
        }

        var equivalencia = value.Equivalencia;
        TipoSeleccionado = TiposDisponibles.First(t => t.Tipo == equivalencia.Tipo);
        TextoRubro = equivalencia.TextoRubro ?? string.Empty;
        Texto = equivalencia.Texto;
        int? idDestino = equivalencia.IdCategoria ?? equivalencia.IdMarca;
        DestinoSeleccionado = Destinos.FirstOrDefault(d => d.IdDestino == idDestino) ?? OpcionIgnorar;
    }

    private bool PuedeAsignar => PuedeEditar && !IsBusy && !string.IsNullOrWhiteSpace(Texto);

    /// <summary>Crea la equivalencia o reemplaza su destino; con "Ignorar" el valor deja de ofrecerse para traducir.</summary>
    [RelayCommand(CanExecute = nameof(PuedeAsignar))]
    private async Task AsignarAsync()
    {
        if (Proveedor is null)
        {
            return;
        }

        var dto = new AsignarEquivalenciaDto
        {
            IdProveedor = Proveedor.IdProveedor,
            Tipo = TipoSeleccionado.Tipo,
            TextoRubro = EsTipoCategoria && !string.IsNullOrWhiteSpace(TextoRubro) ? TextoRubro.Trim() : null,
            Texto = Texto.Trim(),
            IdDestino = DestinoSeleccionado?.IdDestino
        };

        bool guardada = await EscribirAsync(
            "guardar la equivalencia",
            (idActor, token) => _equivalenciaService.AsignarAsync(idActor, dto, token));
        if (guardada)
        {
            LimpiarFormulario();
            MensajeEstado = "Equivalencia guardada.";
        }
    }

    private bool PuedeQuitar => PuedeEditar && !IsBusy;

    /// <summary>Da de baja la equivalencia (con confirmación): la próxima importación vuelve a ofrecer el valor.</summary>
    [RelayCommand(CanExecute = nameof(PuedeQuitar))]
    private async Task QuitarAsync(EquivalenciaProveedorFila? fila)
    {
        if (fila is null || Proveedor is null)
        {
            return;
        }

        MensajeError = null;
        MensajeEstado = null;

        bool confirmado = _dialogService.Confirmar(
            "Quitar equivalencia",
            $"¿Quitar la equivalencia de \"{fila.TextoProveedor}\"?\nLa próxima importación volverá a ofrecer ese valor para traducirlo.");
        if (!confirmado)
        {
            return;
        }

        var dto = new QuitarEquivalenciaDto
        {
            IdProveedor = Proveedor.IdProveedor,
            Tipo = fila.Equivalencia.Tipo,
            TextoRubro = fila.Equivalencia.TextoRubro,
            Texto = fila.Equivalencia.Texto
        };

        bool quitada = await EscribirAsync(
            "quitar la equivalencia",
            (idActor, token) => _equivalenciaService.QuitarAsync(idActor, dto, token));
        if (quitada)
        {
            MensajeEstado = "Equivalencia quitada.";
        }
    }

    /// <summary>
    /// Ejecuta una escritura en segundo plano (Ley 4) y, si salió bien, vuelve a leer las equivalencias.
    /// Una excepción de dominio o de validación se muestra sin cerrar el diálogo.
    /// </summary>
    private async Task<bool> EscribirAsync(string accion, Func<int, CancellationToken, Task> escritura)
    {
        MensajeError = null;
        MensajeEstado = null;
        int idActor = _session.IdUsuario ?? 0;
        bool exito = false;

        IsBusy = true;
        try
        {
            await _carga.EjecutarOperacionAsync(
                async token =>
                {
                    await Task.Run(() => escritura(idActor, token), token);
                    return true;
                },
                _ => exito = true,
                ex => InformarError(accion, ex));

            if (exito)
            {
                await RecargarEquivalenciasAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }

        return exito;
    }

    private Task RecargarEquivalenciasAsync()
    {
        int idProveedor = Proveedor!.IdProveedor;
        return _carga.EjecutarAsync(
            token => Task.Run(() => _equivalenciaService.ListarAsync(idProveedor, null, token), token),
            AplicarEquivalencias,
            ex => InformarError("recargar las equivalencias", ex));
    }

    private void AplicarEquivalencias(IReadOnlyList<EquivalenciaDto> equivalencias)
    {
        FilaSeleccionada = null;
        Equivalencias.Clear();
        foreach (var equivalencia in equivalencias)
        {
            Equivalencias.Add(new EquivalenciaProveedorFila(equivalencia));
        }
    }

    // El destino depende del tipo: categorías del comercio o marcas. "Ignorar" va siempre primero (M-32).
    private void ReconstruirDestinos()
    {
        Destinos.Clear();
        Destinos.Add(OpcionIgnorar);
        if (TipoSeleccionado.Tipo == TipoEquivalenciaEnum.Categoria)
        {
            foreach (var categoria in _categorias)
            {
                Destinos.Add(new OpcionDestinoEquivalencia(categoria.IdCategoria, categoria.NombreCategoria));
            }
        }
        else
        {
            foreach (var marca in _marcas)
            {
                Destinos.Add(new OpcionDestinoEquivalencia(marca.IdMarca, marca.NombreMarca));
            }
        }

        DestinoSeleccionado = OpcionIgnorar;
    }

    private void LimpiarFormulario()
    {
        FilaSeleccionada = null;
        TextoRubro = string.Empty;
        Texto = string.Empty;
        DestinoSeleccionado = OpcionIgnorar;
    }

    private void InformarError(string accion, Exception ex)
    {
        if (ex is ValidationException validacion)
        {
            MensajeError = string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage));
            return;
        }

        if (ex is DomainException)
        {
            // Incluye OperacionNoAutorizadaException (D-5) y la carrera de dos usuarios sobre la misma equivalencia.
            _logger.LogWarning(ex, "Regla de negocio al {Accion}: {Mensaje}", accion, ex.Message);
            MensajeError = ex.Message;
            return;
        }

        _logger.LogError(ex, "Error inesperado al {Accion}: {Mensaje}", accion, ex.Message);
        MensajeError = $"Error inesperado al {accion}: {ex.Message}";
    }

    /// <summary>Cancela las operaciones en curso al cerrar el diálogo.</summary>
    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}
