using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Services;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Clientes;

/// <summary>
/// ViewModel principal para la administración del padrón de clientes y consulta de saldos de cuentas corrientes (RF-20).
/// </summary>
public partial class ClientesViewModel : ObservableObject
{
    private readonly IClienteService _clienteService;
    private readonly IClienteDialogService _dialogService;
    private readonly ILogger<ClientesViewModel> _logger;

    private List<ClienteDto> _cacheClientes = new();

    public ObservableCollection<ClienteDto> Clientes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayClienteSeleccionado))]
    [NotifyPropertyChangedFor(nameof(PuedeCobrar))]
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
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        PaginaActual = 1;
        AplicarFiltrosLocales();
    }

    partial void OnCondicionIvaFiltroChanged(CondicionIvaEnum? value)
    {
        PaginaActual = 1;
        AplicarFiltrosLocales();
    }

    partial void OnSoloConDeudaChanged(bool value)
    {
        PaginaActual = 1;
        AplicarFiltrosLocales();
    }

    partial void OnSoloConCuentaCorrienteChanged(bool value)
    {
        PaginaActual = 1;
        AplicarFiltrosLocales();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        AplicarFiltrosLocales();
    }

    [RelayCommand]
    public async Task CargarClientesAsync()
    {
        try
        {
            IsBusy = true;
            MensajeError = null;
            MensajeEstado = "Cargando padrón de clientes...";

            var clientes = await _clienteService.BuscarClientesAsync(string.Empty);
            _cacheClientes = clientes.ToList();

            TotalClientes = _cacheClientes.Count;
            TotalClientesConDeuda = _cacheClientes.Count(c => c.SaldoCuentaCorriente > 0m);
            TotalDeudaCartera = _cacheClientes.Sum(c => c.SaldoCuentaCorriente);

            PaginaActual = 1;
            AplicarFiltrosLocales();

            MensajeEstado = $"Se cargaron {TotalClientes} clientes (Deuda total: {TotalDeudaCartera:C}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar clientes: {Mensaje}", ex.Message);
            MensajeError = $"Error al cargar clientes: {ex.Message}";
            _dialogService.MostrarError("Error de Carga", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void NuevoCliente()
    {
        try
        {
            _dialogService.MostrarDialogoCrear(async dto =>
            {
                var nuevo = await _clienteService.CrearClienteAsync(dto);
                _cacheClientes.Add(nuevo);
                _cacheClientes = _cacheClientes.OrderBy(c => c.RazonSocialONombre).ToList();

                ActualizarMetricas();
                AplicarFiltrosLocales();

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

                int idx = _cacheClientes.FindIndex(c => c.IdCliente == dto.IdCliente);
                if (idx >= 0)
                {
                    var actualizado = await _clienteService.ObtenerClientePorIdAsync(dto.IdCliente);
                    if (actualizado != null)
                    {
                        _cacheClientes[idx] = actualizado;
                    }
                }

                _cacheClientes = _cacheClientes.OrderBy(c => c.RazonSocialONombre).ToList();
                ActualizarMetricas();
                AplicarFiltrosLocales();

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

            _cacheClientes.RemoveAll(c => c.IdCliente == cliente.IdCliente);
            ActualizarMetricas();
            AplicarFiltrosLocales();

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
            var index = _cacheClientes.FindIndex(c => c.IdCliente == cliente.IdCliente);
            if (index >= 0)
            {
                var actualizado = _cacheClientes[index] with
                {
                    SaldoCuentaCorriente = resultado.NuevoSaldo
                };
                _cacheClientes[index] = actualizado;

                ActualizarMetricas();
                AplicarFiltrosLocales();
                ClienteSeleccionado = Clientes.FirstOrDefault(c => c.IdCliente == cliente.IdCliente);
            }

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
    public void PaginaSiguiente()
    {
        if (PuedeAvanzarPagina)
        {
            PaginaActual++;
            ActualizarPaginaActual();
        }
    }

    [RelayCommand]
    public void PaginaAnterior()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            ActualizarPaginaActual();
        }
    }

    [RelayCommand]
    public void PrimeraPagina()
    {
        if (PaginaActual != 1)
        {
            PaginaActual = 1;
            ActualizarPaginaActual();
        }
    }

    [RelayCommand]
    public void UltimaPagina()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            ActualizarPaginaActual();
        }
    }

    private List<ClienteDto> _cacheFiltrados = new();

    private void AplicarFiltrosLocales()
    {
        var filtrados = _cacheClientes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            string termino = TextoBusqueda.Trim();
            filtrados = filtrados.Where(c =>
                c.RazonSocialONombre.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                c.NumeroDocumento.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                (c.Email != null && c.Email.Contains(termino, StringComparison.OrdinalIgnoreCase)));
        }

        if (CondicionIvaFiltro.HasValue)
        {
            filtrados = filtrados.Where(c => c.CondicionIva == CondicionIvaFiltro.Value);
        }

        if (SoloConDeuda)
        {
            filtrados = filtrados.Where(c => c.SaldoCuentaCorriente > 0m);
        }

        if (SoloConCuentaCorriente)
        {
            filtrados = filtrados.Where(c => c.TieneCuentaCorriente);
        }

        _cacheFiltrados = filtrados.OrderBy(c => c.RazonSocialONombre).ToList();
        TotalRegistrosFiltrados = _cacheFiltrados.Count;

        TotalPaginas = Math.Max(1, (int)Math.Ceiling((double)TotalRegistrosFiltrados / Math.Max(1, TamanoPagina)));

        if (PaginaActual > TotalPaginas)
        {
            PaginaActual = TotalPaginas;
        }
        else if (PaginaActual < 1)
        {
            PaginaActual = 1;
        }

        ActualizarPaginaActual();
    }

    private void ActualizarPaginaActual()
    {
        var paginaItems = _cacheFiltrados
            .Skip((PaginaActual - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .ToList();

        Clientes.Clear();
        foreach (var c in paginaItems)
        {
            Clientes.Add(c);
        }

        OnPropertyChanged(nameof(PuedeRetrocederPagina));
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));
    }

    private void ActualizarMetricas()
    {
        TotalClientes = _cacheClientes.Count;
        TotalClientesConDeuda = _cacheClientes.Count(c => c.SaldoCuentaCorriente > 0m);
        TotalDeudaCartera = _cacheClientes.Sum(c => c.SaldoCuentaCorriente);
    }
}
