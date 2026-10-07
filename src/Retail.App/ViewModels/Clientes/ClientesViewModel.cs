using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.App.Services;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Clientes;

/// <summary>
/// ViewModel principal para la administración del padrón de clientes y gestión de cuentas corrientes con push-down a SQL Server (RF-20, Ley 8).
/// </summary>
public partial class ClientesViewModel : ObservableObject, IDisposable
{
    private readonly IClienteService _clienteService;
    private readonly IClienteDialogService _dialogService;
    private readonly ILogger<ClientesViewModel> _logger;

    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    // Búsqueda, filtros y paginación comparten esta carga: nunca usan el DbContext de la pantalla al mismo
    // tiempo, solo la vigente actualiza la grilla y al salir de la pantalla se cancela (H-19).
    private readonly CargaSerializada _cargaClientes = new();

    public ObservableCollection<ClienteDto> Clientes { get; } = new();

    public IReadOnlyList<CondicionIvaEnum?> CondicionesIvaDisponibles { get; } =
    [
        null,
        CondicionIvaEnum.ResponsableInscripto,
        CondicionIvaEnum.Monotributo,
        CondicionIvaEnum.Exento,
        CondicionIvaEnum.ConsumidorFinal
    ];

    [ObservableProperty]
    private ClienteDto? _clienteSeleccionado;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private CondicionIvaEnum? _condicionIvaFiltro;

    [ObservableProperty]
    private bool _soloConDeuda;

    [ObservableProperty]
    private bool _soloConCuentaCorriente;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private int _totalClientes;

    [ObservableProperty]
    private int _totalClientesConDeuda;

    [ObservableProperty]
    private decimal _totalDeudaCartera;

    [ObservableProperty]
    private int _paginaActual = 1;

    [ObservableProperty]
    private int _tamanoPagina = 10;

    [ObservableProperty]
    private int _totalPaginas = 1;

    [ObservableProperty]
    private int _totalRegistrosFiltrados;

    public bool HayClienteSeleccionado => ClienteSeleccionado != null;

    public bool PuedeCobrar => ClienteSeleccionado is { SaldoCuentaCorriente: > 0m };

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalRegistrosFiltrados == 0
        ? "Sin clientes registrados"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistrosFiltrados)} de {TotalRegistrosFiltrados} clientes";

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = [10, 20, 50];

    public ClientesViewModel(
        IClienteService clienteService,
        IClienteDialogService dialogService,
        ILogger<ClientesViewModel>? logger = null)
    {
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? NullLogger<ClientesViewModel>.Instance;
    }

    partial void OnClienteSeleccionadoChanged(ClienteDto? value)
    {
        OnPropertyChanged(nameof(HayClienteSeleccionado));
        OnPropertyChanged(nameof(PuedeCobrar));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        PaginaActual = 1;
        _ = CargarClientesConEsperaAsync(EsperaBusqueda);
    }

    partial void OnCondicionIvaFiltroChanged(CondicionIvaEnum? value)
    {
        PaginaActual = 1;
        _ = CargarClientesAsync();
    }

    partial void OnSoloConDeudaChanged(bool value)
    {
        PaginaActual = 1;
        _ = CargarClientesAsync();
    }

    partial void OnSoloConCuentaCorrienteChanged(bool value)
    {
        PaginaActual = 1;
        _ = CargarClientesAsync();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        _ = CargarClientesAsync();
    }

    [RelayCommand]
    public Task CargarClientesAsync(CancellationToken cancellationToken = default)
    {
        return CargarClientesConEsperaAsync(TimeSpan.Zero, cancellationToken);
    }

    private async Task CargarClientesConEsperaAsync(TimeSpan espera, CancellationToken cancellationToken = default)
    {
        var consulta = new ConsultaClientesDto
        {
            TerminoBusqueda = TextoBusqueda,
            CondicionIva = CondicionIvaFiltro,
            SoloConDeuda = SoloConDeuda,
            SoloConCuentaCorriente = SoloConCuentaCorriente,
            Pagina = PaginaActual,
            TamanoPagina = TamanoPagina
        };

        IsBusy = true;
        MensajeError = null;
        MensajeEstado = "Cargando padrón de clientes...";

        await _cargaClientes.EjecutarAsync(
            token => Task.Run(() => _clienteService.ListarClientesPaginadosAsync(consulta, token), token),
            MostrarClientes,
            ex =>
            {
                _logger.LogError(ex, "Error al cargar clientes: {Mensaje}", ex.Message);
                MensajeError = $"Error al cargar clientes: {ex.Message}";
                _dialogService.MostrarError("Error de Carga", ex.Message);
            },
            espera,
            cancellationToken);

        IsBusy = _cargaClientes.EnCurso;
    }

    private void MostrarClientes(ClientesPaginadosDto resultado)
    {
        EjecutarEnDispatcher(() =>
        {
            Clientes.Clear();
            foreach (var c in resultado.Items)
            {
                Clientes.Add(c);
            }
        });

        TotalClientes = resultado.TotalClientes;
        TotalClientesConDeuda = resultado.TotalClientesConDeuda;
        TotalDeudaCartera = resultado.TotalDeudaCartera;
        TotalRegistrosFiltrados = resultado.TotalRegistros;
        TotalPaginas = resultado.TotalPaginas;

        OnPropertyChanged(nameof(PuedeRetrocederPagina));
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));

        MensajeEstado = $"Mostrando {Clientes.Count} de {TotalClientes} clientes (Deuda total: {TotalDeudaCartera:C}).";
    }

    [RelayCommand]
    public void NuevoCliente()
    {
        try
        {
            _dialogService.MostrarDialogoCrear(async dto =>
            {
                var nuevo = await _clienteService.CrearClienteAsync(dto);
                await CargarClientesAsync();
                ClienteSeleccionado = Clientes.FirstOrDefault(c => c.IdCliente == nuevo.IdCliente);
                MensajeEstado = $"Cliente '{nuevo.RazonSocialONombre}' registrado correctamente.";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar cliente: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error al Guardar", ex.Message);
        }
    }

    [RelayCommand]
    public void EditarCliente()
    {
        if (ClienteSeleccionado == null)
        {
            return;
        }

        try
        {
            var seleccionado = ClienteSeleccionado;
            _dialogService.MostrarDialogoModificar(seleccionado, async dto =>
            {
                await _clienteService.ActualizarClienteAsync(dto);
                await CargarClientesAsync();
                ClienteSeleccionado = Clientes.FirstOrDefault(c => c.IdCliente == dto.IdCliente);
                MensajeEstado = $"Cliente '{dto.RazonSocialONombre}' actualizado correctamente.";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al modificar cliente: {Mensaje}", ex.Message);
            _dialogService.MostrarError("Error al Modificar", ex.Message);
        }
    }

    [RelayCommand]
    public async Task DarDeBajaClienteAsync()
    {
        if (ClienteSeleccionado == null)
        {
            return;
        }

        var cliente = ClienteSeleccionado;

        if (cliente.SaldoCuentaCorriente > 0m)
        {
            _dialogService.MostrarError(
                "Operación Denegada",
                $"No es posible dar de baja al cliente '{cliente.RazonSocialONombre}' porque mantiene un saldo deudor pendiente de {cliente.SaldoCuentaCorriente:C}.");
            return;
        }

        bool confirma = _dialogService.Confirmar(
            "Confirmar Baja Lógica",
            $"¿Está seguro de que desea dar de baja al cliente '{cliente.RazonSocialONombre}' ({cliente.NumeroDocumento})?");

        if (!confirma)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _clienteService.BajaClienteAsync(cliente.IdCliente);
            await CargarClientesAsync();
            ClienteSeleccionado = null;
            MensajeEstado = $"Cliente '{cliente.RazonSocialONombre}' dado de baja correctamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al dar de baja cliente ID {Id}: {Mensaje}", cliente.IdCliente, ex.Message);
            _dialogService.MostrarError("Error al Dar de Baja", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CobrarCuentaCorrienteAsync(ClienteDto? clienteParametro = null)
    {
        var cliente = clienteParametro ?? ClienteSeleccionado;
        if (cliente == null)
        {
            _dialogService.MostrarInformacion("Atención", "Debe seleccionar un cliente para registrar una cobranza.");
            return;
        }

        if (cliente.SaldoCuentaCorriente <= 0m)
        {
            _dialogService.MostrarInformacion(
                "Sin Saldo Deudor",
                $"El cliente '{cliente.RazonSocialONombre}' no registra deudas pendientes de cuenta corriente.");
            return;
        }

        var resultado = await _dialogService.MostrarCobranzaModalAsync(cliente);
        if (resultado != null)
        {
            await CargarClientesAsync();
            ClienteSeleccionado = Clientes.FirstOrDefault(c => c.IdCliente == cliente.IdCliente);
            MensajeEstado = $"Cobranza #{resultado.IdCobranza:D6} registrada con éxito. Monto: {resultado.MontoAbonado:C}. Nuevo saldo: {resultado.NuevoSaldo:C}.";
            _dialogService.MostrarInformacion(
                "Cobranza Registrada",
                $"Se registró exitosamente la cobranza #{resultado.IdCobranza:D6} por {resultado.MontoAbonado:C}.\nNuevo saldo: {resultado.NuevoSaldo:C}");
        }
    }

    [RelayCommand]
    public void AlternarSoloConDeuda()
    {
        SoloConDeuda = !SoloConDeuda;
    }

    [RelayCommand]
    public async Task PaginaSiguienteAsync()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            await CargarClientesAsync();
        }
    }

    [RelayCommand]
    public async Task PaginaAnteriorAsync()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            await CargarClientesAsync();
        }
    }

    [RelayCommand]
    public async Task PrimeraPaginaAsync()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            await CargarClientesAsync();
        }
    }

    [RelayCommand]
    public async Task UltimaPaginaAsync()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            await CargarClientesAsync();
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
        _cargaClientes.Dispose();
        GC.SuppressFinalize(this);
    }
}
