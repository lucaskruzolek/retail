using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Clientes;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Ventas;

/// <summary>
/// ViewModel para el diálogo de selección rápida de clientes de mostrador (F4).
/// Permite buscar por DNI/CUIT o Razón Social y conmutar a Consumidor Final de forma inmediata.
/// </summary>
public partial class SeleccionarClienteModalViewModel : ObservableObject
{
    private readonly IClienteService _clienteService;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private ClienteDto? _clienteSeleccionado;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ClienteDto> Clientes { get; } = new();

    public ClienteDto? ClienteElegido { get; private set; }

    public bool OperacionConfirmada { get; private set; }

    public SeleccionarClienteModalViewModel(IClienteService clienteService)
    {
        _clienteService = clienteService ?? throw new ArgumentNullException(nameof(clienteService));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        _ = BuscarClientesAsync(value);
    }

    [RelayCommand]
    public async Task BuscarClientesAsync(string? query = null)
    {
        try
        {
            IsBusy = true;
            string termino = query ?? TextoBusqueda;
            var resultados = await _clienteService.BuscarClientesAsync(termino);

            Clientes.Clear();
            foreach (var cliente in resultados)
            {
                Clientes.Add(cliente);
            }
        }
        finally
        {
            IsBusy = false;
        }
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
}
