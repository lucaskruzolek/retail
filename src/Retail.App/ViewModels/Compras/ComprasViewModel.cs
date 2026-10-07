using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Helpers;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Compras;

/// <summary>
/// ViewModel principal para la gestión de compras a distribuidores e ingreso de facturas/remitos (Módulo 4.2).
/// </summary>
public partial class ComprasViewModel : ObservableObject, IDisposable
{
    // Serializa la carga de proveedores sobre el DbContext de esta pantalla y la cancela al salir de ella (H-19).
    private readonly CargaSerializada _cargaProveedores = new();

    private readonly IProveedorService _proveedorService;
    private readonly ILogger<ComprasViewModel> _logger;

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();
    public ObservableCollection<DetalleCompraItemViewModel> DetalleCompraItems { get; } = new();

    [ObservableProperty]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    private string _numeroComprobante = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    /// <summary>
    /// Total general de la factura de compra acumulando los subtotales de cada renglón.
    /// </summary>
    public decimal TotalCompra => DetalleCompraItems.Sum(i => i.Subtotal);

    public ComprasViewModel(
        IProveedorService proveedorService,
        ILogger<ComprasViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task CargarProveedoresAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        await _cargaProveedores.EjecutarAsync(
            token => Task.Run(() => _proveedorService.ListarProveedoresAsync(token), token),
            lista =>
            {
                Proveedores.Clear();
                if (lista != null)
                {
                    foreach (var p in lista)
                    {
                        Proveedores.Add(p);
                    }
                }
            },
            ex =>
            {
                _logger.LogError(ex, "Error al cargar proveedores en el módulo de compras");
                MensajeError = "No se pudieron cargar los proveedores.";
            },
            cancellationToken: cancellationToken);

        IsBusy = _cargaProveedores.EnCurso;
    }

    [RelayCommand]
    private void AgregarItem()
    {
        // Aquí se integrará próximamente la apertura del diálogo de selección de artículos del catálogo
        MensajeEstado = "Acción de agregar artículo (F2) invocada.";
    }

    [RelayCommand]
    private async Task RegistrarCompraAsync()
    {
        if (ProveedorSeleccionado is null)
        {
            MensajeError = "Debe seleccionar un proveedor o distribuidor para la compra.";
            return;
        }

        if (DetalleCompraItems.Count == 0)
        {
            MensajeError = "La factura de compra debe contener al menos un renglón de artículo.";
            return;
        }

        IsBusy = true;
        MensajeError = null;

        try
        {
            // Lógica de transacción ACID e invocación a ICompraService (Siguiente fase)
            await Task.Delay(500); // Simulación temporal de procesamiento
            MensajeEstado = $"Compra Nro. {NumeroComprobante} registrada exitosamente.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al registrar la compra a proveedor");
            MensajeError = $"Error al registrar la compra: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Lo invoca el scope de la pantalla al salir de ella: cancela la carga en curso para que no use el
    /// DbContext ya descartado ni muestre errores en otra pantalla (H-19).
    /// </summary>
    public void Dispose()
    {
        _cargaProveedores.Dispose();
        GC.SuppressFinalize(this);
    }
}
