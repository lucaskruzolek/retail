using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.App.ViewModels.Comun;
using Retail.Application.DTOs.Listas;
using Retail.Application.Exceptions;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Pestaña Listas (IU-14, RF-30): grilla paginada en SQL con búsqueda por nombre y las acciones de cada fila (editar,
/// imprimir, exportar a PDF y dar de baja), más "Nueva lista". Las listas se venden desde el Punto de venta.
/// </summary>
public partial class ListasViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    private readonly IListaService _listaService;
    private readonly IListaDialogService _dialogService;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ListasViewModel> _logger;
    private readonly FlujoExportarPdfLista _flujoPdf;

    // Una sola fila de accesos para el DbContext de la pantalla (H-19); el editor la comparte.
    private readonly CargaSerializada _carga;

    // Hubo al menos una respuesta del servidor: antes de eso la grilla vacía no significa "no hay listas".
    private bool _huboRespuesta;

    // La página pedida quedó fuera de rango (se dio de baja la última lista de la última página): hay que retroceder.
    private bool _paginaFueraDeRango;

    public ListasViewModel(
        IListaService listaService,
        IListaDialogService dialogService,
        TimeProvider reloj,
        ILogger<ListasViewModel> logger)
    {
        _listaService = listaService ?? throw new ArgumentNullException(nameof(listaService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _carga = new CargaSerializada(_logger);
        _flujoPdf = new FlujoExportarPdfLista(_dialogService, _logger);
    }

    // ------------------------------------------------------------------ //
    // Búsqueda y grilla                                                   //
    // ------------------------------------------------------------------ //

    public ObservableCollection<ListaFila> Listas { get; } = new();

    /// <summary>Búsqueda por nombre (se resuelve en SQL, con una espera para no consultar por cada tecla).</summary>
    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private int _totalRegistros;

    [ObservableProperty]
    private int _tamanoPagina = 15;

    [ObservableProperty]
    private ListaFila? _listaSeleccionada;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = OpcionesPaginacion.Tamanos;

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public bool HayFiltrosActivos => !string.IsNullOrWhiteSpace(TextoBusqueda);

    // ------------------------------------------------------------------ //
    // Estado de la pantalla                                               //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private string? _mensajeError;

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    /// <summary>La consulta terminó sin errores y no devolvió filas: se muestra el estado vacío.</summary>
    public bool EstaVacio => _huboRespuesta && !IsBusy && !TieneMensajeError && Listas.Count == 0;

    /// <summary>Texto del estado vacío: orienta al usuario si no hay listas o dice que la búsqueda no encontró ninguna.</summary>
    public string TextoVacio => HayFiltrosActivos
        ? "Ninguna lista coincide con la búsqueda."
        : "Todavía no hay listas. Cree una con «Nueva lista» o desde el Punto de venta.";

    // ------------------------------------------------------------------ //
    // Carga                                                               //
    // ------------------------------------------------------------------ //

    /// <summary>Carga la primera página. La invoca la vista al mostrarse.</summary>
    public Task InicializarAsync()
    {
        PaginaActual = 1;
        return CargarListasAsync();
    }

    /// <summary>
    /// Consulta la página actual con la búsqueda vigente. <paramref name="espera"/> es un debounce: si llega otra consulta
    /// antes de que venza, esta ni siquiera se ejecuta.
    /// </summary>
    public async Task CargarListasAsync(TimeSpan espera = default, CancellationToken cancellationToken = default)
    {
        var consulta = new ConsultaListasDto
        {
            TerminoBusqueda = TextoBusqueda,
            Pagina = PaginaActual,
            TamanoPagina = TamanoPagina
        };

        IsBusy = true;
        MensajeError = null;

        // Ley 4: la consulta corre fuera del hilo de UI y sobre la fila de accesos de la pantalla.
        await _carga.EjecutarAsync(
            token => Task.Run(() => _listaService.ListarAsync(consulta, token), token),
            MostrarListas,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar las listas de artículos");
                MensajeError = "No se pudieron cargar las listas.";
            },
            espera,
            cancellationToken);

        IsBusy = _carga.EnCurso;

        if (_paginaFueraDeRango)
        {
            _paginaFueraDeRango = false;
            PaginaActual = TotalPaginas;
            await CargarListasAsync(cancellationToken: cancellationToken);
        }
    }

    [RelayCommand]
    private Task RecargarAsync()
    {
        return CargarListasAsync();
    }

    private void MostrarListas(ListasPaginadosDto resultado)
    {
        Listas.Clear();
        foreach (var resumen in resultado.Items)
        {
            Listas.Add(new ListaFila(resumen, _reloj));
        }

        TotalRegistros = resultado.TotalRegistros;
        TotalPaginas = Math.Max(1, resultado.TotalPaginas);
        _huboRespuesta = true;

        _paginaFueraDeRango = resultado.Items.Count == 0 && PaginaActual > TotalPaginas;

        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(EstaVacio));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        OnPropertyChanged(nameof(HayFiltrosActivos));
        OnPropertyChanged(nameof(TextoVacio));
        LimpiarFiltrosCommand.NotifyCanExecuteChanged();
        RecargarDesdePrimeraPagina(EsperaBusqueda);
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        RecargarDesdePrimeraPagina(TimeSpan.Zero);
    }

    [RelayCommand(CanExecute = nameof(HayFiltrosActivos))]
    public void LimpiarFiltros()
    {
        TextoBusqueda = string.Empty;
    }

    private void RecargarDesdePrimeraPagina(TimeSpan espera)
    {
        PaginaActual = 1;
        _ = CargarListasAsync(espera);
    }

    // ------------------------------------------------------------------ //
    // Paginación                                                          //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarListasAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarListasAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarListasAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarListasAsync();
        }
    }

    /// <summary>Va a una página puntual (los números de la barra de paginación); fuera de rango no hace nada.</summary>
    [RelayCommand]
    public async Task IrAPaginaAsync(int pagina)
    {
        if (pagina >= 1 && pagina <= TotalPaginas && pagina != PaginaActual)
        {
            PaginaActual = pagina;
            await CargarListasAsync();
        }
    }

    // ------------------------------------------------------------------ //
    // Acciones                                                            //
    // ------------------------------------------------------------------ //

    /// <summary>Abre el editor con una lista vacía y recarga si se guardó.</summary>
    [RelayCommand]
    private async Task NuevaListaAsync()
    {
        if (_dialogService.AbrirEditor(null, _carga))
        {
            await CargarListasAsync();
        }
    }

    /// <summary>
    /// Lee la lista actual (con la fecha de modificación que viaja como control optimista) y abre el editor. Lo disparan
    /// el botón de la fila, el doble clic y Enter. Recarga si se guardó.
    /// </summary>
    [RelayCommand]
    private async Task EditarAsync(ListaFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        ListaDto? lista;
        IsBusy = true;
        try
        {
            bool completado;
            (completado, lista) = await _carga.EjecutarEnFilaAsync(token => _listaService.ObtenerAsync(fila.IdLista, token));
            if (!completado)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            ManejarError(ex);
            return;
        }
        finally
        {
            IsBusy = _carga.EnCurso;
        }

        if (lista is null)
        {
            _dialogService.MostrarAlerta("La lista ya no existe", "Otra terminal la dio de baja. Se actualizaron los datos.");
            await CargarListasAsync();
            return;
        }

        if (_dialogService.AbrirEditor(lista, _carga))
        {
            await CargarListasAsync();
        }
    }

    [RelayCommand]
    private async Task ImprimirAsync(ListaFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _carga.EjecutarOperacionEnFilaAsync(token => _listaService.ImprimirAsync(fila.IdLista, token));
            _dialogService.MostrarInformacion("Lista impresa", $"Se envió la lista «{fila.Nombre}» a la impresora de tickets.");
        }
        catch (Exception ex)
        {
            ManejarError(ex);
        }
        finally
        {
            IsBusy = _carga.EnCurso;
        }
    }

    [RelayCommand]
    private async Task ExportarPdfAsync(ListaFila? fila)
    {
        if (fila is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _flujoPdf.EjecutarAsync(
                fila.Nombre,
                destino => _carga.EjecutarOperacionEnFilaAsync(token => _listaService.ExportarPdfAsync(fila.IdLista, destino, token)));
        }
        catch (Exception ex)
        {
            ManejarError(ex);
        }
        finally
        {
            IsBusy = _carga.EnCurso;
        }
    }

    /// <summary>
    /// Da de baja lógicamente una lista tras pedir confirmación (Ley 3, L-17) y recarga la página actual, haya salido bien
    /// o no: si otra terminal la dio de baja antes, la pantalla tiene que mostrar lo que hay en la base.
    /// </summary>
    [RelayCommand]
    private async Task EliminarAsync(ListaFila? fila)
    {
        if (fila is null || !_dialogService.ConfirmarBaja(fila.Nombre))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _carga.EjecutarOperacionEnFilaAsync(token => _listaService.EliminarAsync(fila.IdLista, token));
        }
        catch (Exception ex)
        {
            ManejarError(ex);
        }

        await CargarListasAsync();
    }

    /// <summary>Traduce cada error a lo que el usuario necesita saber; el orden importa (la validación no es de dominio).</summary>
    private void ManejarError(Exception ex)
    {
        switch (ex)
        {
            case ValidationException validacion:
                _dialogService.MostrarAlerta(
                    "No se pudo completar la operación",
                    string.Join("\n", validacion.Errors.Select(e => e.ErrorMessage).Distinct()));
                break;

            case ConflictoDeConcurrenciaException:
                _dialogService.MostrarAlerta(
                    "Lista modificada en otra terminal",
                    "Otra terminal modificó esta lista mientras la revisaba. Se actualizaron los datos: revíselos y repita la operación.");
                break;

            case DomainException dominio:
                _dialogService.MostrarAlerta("No se pudo completar la operación", dominio.Message);
                break;

            default:
                _logger.LogError(ex, "Error inesperado al operar sobre una lista de artículos");
                _dialogService.MostrarError(
                    "Error inesperado",
                    "No se pudo completar la operación. El detalle quedó registrado en el log.");
                break;
        }
    }

    /// <summary>Lo invoca el scope de la pantalla al salir de ella: cancela lo pendiente (H-19).</summary>
    public void Dispose()
    {
        _carga.Dispose();
        GC.SuppressFinalize(this);
    }
}
