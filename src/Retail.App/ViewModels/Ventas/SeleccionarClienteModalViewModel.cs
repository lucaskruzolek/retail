using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Retail.App.Helpers;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// ViewModel para el diálogo de selección rápida de clientes de mostrador (F4).
/// Permite buscar por DNI/CUIT o Razón Social y conmutar a Consumidor Final de forma inmediata.
/// </summary>
public partial class SeleccionarClienteModalViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan EsperaBusqueda = TimeSpan.FromMilliseconds(250);

    private readonly IClienteService _clienteService;
    private readonly ILogger<SeleccionarClienteModalViewModel> _logger;

    // Cada tecla dispara una búsqueda: esta fila evita que se pisen sobre el DbContext de la pantalla del POS,
    // descarta las respuestas obsoletas y cancela lo pendiente al cerrarse el diálogo (H-19).
    private readonly CargaSerializada _busqueda = new();

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private ClienteDto? _clienteSeleccionado;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ClienteDto> Clientes { get; } = new();

    public ClienteDto? ClienteElegido { get; private set; }

    public bool OperacionConfirmada { get; private set; }

    public SeleccionarClienteModalViewModel(
        IClienteService clienteService,
        ILogger<SeleccionarClienteModalViewModel>? logger = null)
    {
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
        _logger = logger ?? NullLogger<SeleccionarClienteModalViewModel>.Instance;
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        _ = BuscarClientesConEsperaAsync(value, EsperaBusqueda);
    }

    [RelayCommand]
    public Task BuscarClientesAsync(string? query = null)
    {
        return BuscarClientesConEsperaAsync(query ?? TextoBusqueda, TimeSpan.Zero);
    }

    private async Task BuscarClientesConEsperaAsync(string termino, TimeSpan espera)
    {
        IsBusy = true;

        await _busqueda.EjecutarAsync(
            token => Task.Run(() => _clienteService.BuscarClientesAsync(termino, token), token),
            resultados =>
            {
                Clientes.Clear();
                foreach (var cliente in resultados)
                {
                    Clientes.Add(cliente);
                }
            },
            ex => _logger.LogError(ex, "Error al buscar clientes desde el mostrador: {Termino}", termino),
            espera);

        IsBusy = _busqueda.EnCurso;
    }

    [RelayCommand]
    public void SeleccionarCliente(ClienteDto? cliente)
    {
        ClienteElegido = cliente;
        OperacionConfirmada = true;
    }

    [RelayCommand]
    public void EstablecerConsumidorFinal()
    {
        ClienteElegido = null; // null representa Consumidor Final
        OperacionConfirmada = true;
    }

    /// <summary>
    /// Lo invoca el servicio de diálogos al cerrarse el modal: cancela la búsqueda pendiente.
    /// </summary>
    public void Dispose()
    {
        _busqueda.Dispose();
        GC.SuppressFinalize(this);
    }
}
