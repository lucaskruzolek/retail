using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Presupuestos;

/// <summary>
/// Diálogo "Recuperar presupuesto" del Punto de venta (RF-12): un selector con buscador y lista de los presupuestos
/// <b>pendientes</b> (vigentes o vencidos; los convertidos y anulados ya no se pueden cobrar). El cajero reconoce el
/// presupuesto en la lista en lugar de recordar su número, y si el ticket ya tiene un cliente la lista arranca filtrada
/// por él. La primera fila queda seleccionada después de cada carga: escribir el número y presionar Enter alcanza.
/// </summary>
public partial class RecuperarPresupuestoViewModel : ObservableObject
{
    /// <summary>Cantidad máxima de filas por consulta; con más resultados se pide refinar la búsqueda.</summary>
    public const int TamanoPagina = 50;

    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    private readonly IPresupuestoService _presupuestoService;
    private readonly CargaSerializada _acceso;
    private readonly ILogger _logger;
    private readonly TimeProvider _reloj;
    private readonly int? _idCliente;
    private readonly string? _nombreCliente;

    // Última carga lanzada: ConfirmarAsync la espera para no recuperar la fila de una lista que está por reemplazarse.
    private Task _cargaVigente = Task.CompletedTask;

    // Hubo al menos una respuesta del servidor: antes de eso la lista vacía no significa "no hay presupuestos".
    private bool _huboRespuesta;

    /// <param name="acceso">Fila de accesos a datos del POS, para no usar su DbContext en paralelo (H-19).</param>
    /// <param name="idCliente">Cliente del ticket; <c>null</c> es Consumidor Final y no se prefiltra.</param>
    /// <param name="nombreCliente">Nombre del cliente del ticket, para el rótulo del interruptor.</param>
    public RecuperarPresupuestoViewModel(
        IPresupuestoService presupuestoService,
        CargaSerializada acceso,
        ILogger logger,
        int? idCliente = null,
        string? nombreCliente = null,
        TimeProvider? reloj = null)
    {
        _presupuestoService = presupuestoService ?? throw new ArgumentNullException(nameof(presupuestoService));
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reloj = reloj ?? TimeProvider.System;
        _idCliente = idCliente;
        _nombreCliente = nombreCliente;

        // Con cliente en el ticket, arranca prefiltrado (B). Se asigna al campo: la primera carga la hace la vista.
        _soloDelCliente = idCliente is not null;
    }

    public ObservableCollection<PresupuestoFila> Presupuestos { get; } = new();

    /// <summary>Búsqueda por número, título o cliente (se resuelve en SQL, con una espera para no consultar por cada tecla).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoVacio))]
    private string _textoBusqueda = string.Empty;

    /// <summary>Muestra solo los presupuestos del cliente del ticket; apagarlo permite ver los de todos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoVacio))]
    private bool _soloDelCliente;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecuperarCommand))]
    private PresupuestoFila? _presupuestoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecuperarCommand))]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private bool _isBusy;

    /// <summary>La lista no se pudo cargar: reemplaza a la grilla y ofrece reintentar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneErrorCarga))]
    [NotifyPropertyChangedFor(nameof(EstaVacio))]
    private string? _mensajeErrorCarga;

    /// <summary>No se pudo recuperar el presupuesto elegido: se muestra sobre la lista, que sigue a la vista.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMasDeLosMostrados))]
    [NotifyPropertyChangedFor(nameof(TextoLimite))]
    private int _totalRegistros;

    /// <summary>Hay cliente en el ticket: recién entonces el interruptor tiene sentido.</summary>
    public bool HayClienteEnElTicket => _idCliente is not null;

    public string TextoSoloDelCliente => $"Solo de {_nombreCliente}";

    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    public bool TieneErrorCarga => !string.IsNullOrWhiteSpace(MensajeErrorCarga);

    /// <summary>La consulta terminó sin errores y no devolvió filas: se muestra el estado vacío.</summary>
    public bool EstaVacio => _huboRespuesta && !IsBusy && !TieneErrorCarga && Presupuestos.Count == 0;

    public bool HayMasDeLosMostrados => TotalRegistros > TamanoPagina;

    public string TextoLimite => $"Se muestran los {TamanoPagina} más recientes de {TotalRegistros}; refine la búsqueda.";

    public string TextoVacio
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                return "Ningún presupuesto pendiente coincide con la búsqueda.";
            }

            return SoloDelCliente
                ? $"{_nombreCliente} no tiene presupuestos pendientes. Desactive el filtro para ver los de todos."
                : "No hay presupuestos pendientes.";
        }
    }

    /// <summary>Presupuesto recuperado; <c>null</c> si el usuario canceló o todavía no recuperó ninguno.</summary>
    public PresupuestoParaVentaDto? Resultado { get; private set; }

    /// <summary>El diálogo se cierra apenas hay un presupuesto recuperado.</summary>
    public bool Recuperado => Resultado is not null;

    partial void OnTextoBusquedaChanged(string value)
    {
        MensajeError = null;
        _ = CargarAsync(EsperaBusqueda);
    }

    partial void OnSoloDelClienteChanged(bool value)
    {
        MensajeError = null;
        _ = CargarAsync(TimeSpan.Zero);
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
        var consulta = new ConsultaPresupuestosDto
        {
            TerminoBusqueda = TextoBusqueda,
            Estados = [FiltroEstadoPresupuestoEnum.Vigentes, FiltroEstadoPresupuestoEnum.Vencidos],
            IdsCliente = SoloDelCliente && _idCliente is int idCliente ? [idCliente] : [],
            Pagina = 1,
            TamanoPagina = TamanoPagina
        };

        _cargaVigente = EjecutarCargaAsync(consulta, espera);
        return _cargaVigente;
    }

    private async Task EjecutarCargaAsync(ConsultaPresupuestosDto consulta, TimeSpan espera)
    {
        IsBusy = true;
        MensajeErrorCarga = null;

        // Ley 4: la consulta corre fuera del hilo de UI y, por H-19, en la fila de accesos del POS.
        await _acceso.EjecutarAsync(
            token => Task.Run(() => _presupuestoService.ListarAsync(consulta, token), token),
            MostrarPresupuestos,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar los presupuestos pendientes en el Punto de venta");
                Presupuestos.Clear();
                PresupuestoSeleccionado = null;
                MensajeErrorCarga = "No se pudieron cargar los presupuestos.";
            },
            espera);

        IsBusy = _acceso.EnCurso;
    }

    private void MostrarPresupuestos(PresupuestosPaginadosDto resultado)
    {
        Presupuestos.Clear();
        foreach (var resumen in resultado.Items)
        {
            Presupuestos.Add(new PresupuestoFila(resumen, _reloj));
        }

        TotalRegistros = resultado.TotalRegistros;
        _huboRespuesta = true;

        // Primera fila seleccionada: el más reciente, o el único que coincide con lo escrito.
        PresupuestoSeleccionado = Presupuestos.FirstOrDefault();
        OnPropertyChanged(nameof(EstaVacio));
    }

    /// <summary>
    /// Enter en el buscador o en la grilla: espera a que termine la carga pendiente (el cajero puede escribir el número y
    /// presionar Enter antes de que llegue la lista) y recupera la fila seleccionada.
    /// </summary>
    public async Task ConfirmarAsync()
    {
        await _cargaVigente;

        if (RecuperarCommand.CanExecute(null))
        {
            await RecuperarCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeRecuperar))]
    private async Task RecuperarAsync()
    {
        var fila = PresupuestoSeleccionado;
        if (fila is null)
        {
            return;
        }

        int idPresupuesto = fila.IdPresupuesto;
        bool recargar = false;

        IsBusy = true;
        MensajeError = null;

        try
        {
            var (completado, presupuesto) = await _acceso.EjecutarEnFilaAsync(token => _presupuestoService.RecuperarParaVentaAsync(idPresupuesto, token));
            if (completado)
            {
                Resultado = presupuesto;
                OnPropertyChanged(nameof(Recuperado));
            }
        }
        catch (TransicionPresupuestoInvalidaException ex)
        {
            // Convertido o anulado (por ejemplo, desde otra terminal) mientras el diálogo estaba abierto: es información.
            var motivo = ex.EstadoActual == EstadoPresupuestoEnum.Convertido ? "ya se convirtió en una venta" : "está anulado";
            MensajeError = $"El presupuesto N.º {fila.Numero} {motivo}: no se puede recuperar.";
            recargar = true;
        }
        catch (DomainException ex)
        {
            // Entre otros, "No se encontró el presupuesto N".
            MensajeError = ex.Message;
            recargar = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al recuperar el presupuesto {IdPresupuesto} en el Punto de venta", idPresupuesto);
            MensajeError = "No se pudo recuperar el presupuesto. El detalle quedó registrado en el log.";
        }
        finally
        {
            IsBusy = false;
        }

        if (recargar)
        {
            // La lista quedó desactualizada: se vuelve a consultar, y el motivo sigue visible sobre ella.
            await CargarAsync(TimeSpan.Zero);
        }
    }

    private bool PuedeRecuperar()
    {
        return !IsBusy && PresupuestoSeleccionado is not null;
    }
}
