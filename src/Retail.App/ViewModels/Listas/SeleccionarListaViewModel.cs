using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Listas;

/// <summary>
/// Diálogo "Cargar → Lista" del Punto de venta (L-05): un selector con buscador y la lista de las listas de artículos
/// con su total a los precios de hoy y la cantidad de ventas. Mismo patrón que el selector de presupuestos: la primera
/// fila queda seleccionada después de cada carga, así escribir el nombre y presionar Enter alcanza. Solo elige: la lectura
/// de los renglones para vender la hace el POS (<c>ObtenerParaVentaAsync</c>).
/// </summary>
public partial class SeleccionarListaViewModel : ObservableObject
{
    /// <summary>Cantidad máxima de filas por consulta; con más resultados se pide refinar la búsqueda.</summary>
    public const int TamanoPagina = 50;

    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    private readonly IListaService _listaService;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;
    private readonly TimeProvider _reloj;

    // Última carga lanzada: Confirmar la espera para no elegir la fila de una lista que está por reemplazarse.
    private Task _cargaVigente = Task.CompletedTask;

    // Hubo al menos una respuesta del servidor: antes de eso la lista vacía no significa "no hay listas".
    private bool _huboRespuesta;

    /// <param name="acceso">Fila de accesos a datos del POS, para no usar su DbContext en paralelo (H-19).</param>
    public SeleccionarListaViewModel(
        IListaService listaService,
        CargaSerializada acceso,
        ILogger logger,
        TimeProvider? reloj = null)
    {
        _listaService = listaService ?? throw new ArgumentNullException(nameof(listaService));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reloj = reloj ?? TimeProvider.System;
    }

    public ObservableCollection<ListaFila> Listas { get; } = new();

    /// <summary>Búsqueda por nombre (se resuelve en SQL, con una espera para no consultar por cada tecla).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoVacio))]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirCommand))]
    private ListaFila? _listaSeleccionada;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirCommand))]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private bool _isBusy;

    /// <summary>La lista no se pudo cargar: reemplaza a la grilla y ofrece reintentar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneErrorCarga))]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private string? _mensajeErrorCarga;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMasDeLosMostrados))]
    [NotifyPropertyChangedFor(nameof(TextoLimite))]
    private int _totalRegistros;

    public bool TieneErrorCarga => !string.IsNullOrWhiteSpace(MensajeErrorCarga);

    /// <summary>La consulta terminó sin errores y no devolvió filas: se muestra el estado vacío.</summary>
    public bool EstaVacio => _huboRespuesta && !IsBusy && !TieneErrorCarga && Listas.Count == 0;

    public bool HayMasDeLosMostrados => TotalRegistros > TamanoPagina;

    public string TextoLimite => $"Se muestran las {TamanoPagina} primeras de {TotalRegistros}; refine la búsqueda.";

    public string TextoVacio => string.IsNullOrWhiteSpace(TextoBusqueda)
        ? "Todavía no hay listas. Se crean desde el POS con Guardar como → Lista o en la pestaña Listas."
        : "Ninguna lista coincide con la búsqueda.";

    /// <summary>Número de la lista elegida; <c>null</c> si el usuario canceló o todavía no eligió ninguna.</summary>
    public int? IdListaElegida { get; private set; }

    /// <summary>El diálogo se cierra apenas hay una lista elegida.</summary>
    public bool Elegida => IdListaElegida is not null;

    partial void OnTextoBusquedaChanged(string value)
    {
        _ = CargarAsync(EsperaBusqueda);
    }

    /// <summary>Primera carga de la lista; la invoca la vista al mostrarse (no se consulta en el constructor).</summary>
    public Task CargarInicialAsync()
    {
        return CargarAsync(TimeSpan.Zero);
    }

    [RelayCommand]
    private Task RecargarAsync()
    {
        return CargarAsync(TimeSpan.Zero);
    }

    private Task CargarAsync(TimeSpan espera)
    {
        var consulta = new ConsultaListasDto
        {
            TerminoBusqueda = TextoBusqueda,
            Pagina = 1,
            TamanoPagina = TamanoPagina
        };

        _cargaVigente = EjecutarCargaAsync(consulta, espera);
        return _cargaVigente;
    }

    private async Task EjecutarCargaAsync(ConsultaListasDto consulta, TimeSpan espera)
    {
        IsBusy = true;
        MensajeErrorCarga = null;

        // Ley 4: la consulta corre fuera del hilo de UI y, por H-19, en la fila de accesos del POS.
        await _acceso.EjecutarAsync(
            token => Task.Run(() => _listaService.ListarAsync(consulta, token), token),
            MostrarListas,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar las listas de artículos en el Punto de venta");
                Listas.Clear();
                ListaSeleccionada = null;
                MensajeErrorCarga = "No se pudieron cargar las listas.";
            },
            espera);

        IsBusy = _acceso.EnCurso;
    }

    private void MostrarListas(ListasPaginadosDto resultado)
    {
        Listas.Clear();
        foreach (var resumen in resultado.Items)
        {
            Listas.Add(new ListaFila(resumen, _reloj));
        }

        TotalRegistros = resultado.TotalRegistros;
        _huboRespuesta = true;

        // Primera fila seleccionada: la primera en el orden del servidor, o la única que coincide con lo escrito.
        ListaSeleccionada = Listas.FirstOrDefault();
        OnPropertyChanged(nameof(EstaVacio));
    }

    /// <summary>
    /// Enter en el buscador o en la grilla: espera a que termine la carga pendiente (el cajero puede escribir el nombre y
    /// presionar Enter antes de que llegue la lista) y elige la fila seleccionada.
    /// </summary>
    public async Task ConfirmarAsync()
    {
        await _cargaVigente;

        if (ElegirCommand.CanExecute(null))
        {
            ElegirCommand.Execute(null);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeElegir))]
    private void Elegir()
    {
        if (ListaSeleccionada is not { } fila)
        {
            return;
        }

        IdListaElegida = fila.IdLista;
        OnPropertyChanged(nameof(Elegida));
    }

    private bool PuedeElegir()
    {
        return !IsBusy && ListaSeleccionada is not null;
    }
}
