using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Retail.App.Services;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Exceptions;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel principal para la administración del padrón de proveedores y distribuidores (Etapa 2.2).
/// Sigue el patrón MVVM de CommunityToolkit con carga asíncrona y delegación de diálogos a IProveedorDialogService.
/// </summary>
public partial class ProveedoresViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;
    private readonly IProveedorDialogService _dialogService;
    private readonly ILogger<ProveedoresViewModel> _logger;

    private List<ProveedorDto> _cacheProveedores = new();

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();

    [ObservableProperty]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _mensajeEstado;

    [ObservableProperty]
    private string? _mensajeError;

    [ObservableProperty]
    private int _totalProveedores;

    public bool HayProveedorSeleccionado => ProveedorSeleccionado != null;

    public ProveedoresViewModel(
        IProveedorService proveedorService,
        IProveedorDialogService dialogService,
        ILogger<ProveedoresViewModel> logger)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ------------------------------------------------------------------ //
    // Propiedades con notificación encadenada                             //
    // ------------------------------------------------------------------ //

    partial void OnProveedorSeleccionadoChanged(ProveedorDto? value)
    {
        OnPropertyChanged(nameof(HayProveedorSeleccionado));
        EditarProveedorCommand.NotifyCanExecuteChanged();
        EliminarProveedorCommand.NotifyCanExecuteChanged();
        AbrirImportadorCommand.NotifyCanExecuteChanged();
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        AplicarFiltroLocal();
    }

    // ------------------------------------------------------------------ //
    // Comandos                                                            //
    // ------------------------------------------------------------------ //

    [RelayCommand]
    private async Task CargarProveedoresAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        MensajeError = null;
        MensajeEstado = null;

        try
        {
            var lista = await Task.Run(
                () => _proveedorService.ListarProveedoresAsync(cancellationToken),
                cancellationToken);

            _cacheProveedores = lista.ToList();
            AplicarFiltroLocal();
            TotalProveedores = _cacheProveedores.Count;
        }
        catch (OperationCanceledException)
        {
            // Ignorar cancelación silenciosa
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar proveedores");
            MensajeError = "No se pudo cargar la lista de proveedores. Intente nuevamente.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NuevoProveedorAsync()
    {
        var creado = await _dialogService.AbrirFormularioNuevoProveedorAsync();
        if (creado)
        {
            MensajeEstado = "Proveedor creado exitosamente.";
            await CargarProveedoresAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HayProveedorSeleccionado))]
    private async Task EditarProveedorAsync()
    {
        if (ProveedorSeleccionado is null) return;

        var editado = await _dialogService.AbrirFormularioEditarProveedorAsync(ProveedorSeleccionado);
        if (editado)
        {
            MensajeEstado = "Proveedor actualizado exitosamente.";
            await CargarProveedoresAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HayProveedorSeleccionado))]
    private async Task EliminarProveedorAsync()
    {
        if (ProveedorSeleccionado is null) return;

        var confirmado = await _dialogService.ConfirmarEliminacionAsync(ProveedorSeleccionado.RazonSocial);
        if (!confirmado) return;

        IsBusy = true;
        MensajeError = null;

        try
        {
            await Task.Run(() => _proveedorService.BajaProveedorAsync(ProveedorSeleccionado.IdProveedor));
            MensajeEstado = $"Proveedor \"{ProveedorSeleccionado.RazonSocial}\" eliminado.";
            ProveedorSeleccionado = null;
            await CargarProveedoresAsync();
        }
        catch (DomainException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar proveedor");
            MensajeError = "No se pudo eliminar el proveedor.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HayProveedorSeleccionado))]
    private async Task AbrirImportadorAsync()
    {
        if (ProveedorSeleccionado is null) return;
        await _dialogService.AbrirImportadorCatalogosAsync(ProveedorSeleccionado);
    }

    // ------------------------------------------------------------------ //
    // Lógica de Filtro Local                                              //
    // ------------------------------------------------------------------ //

    private void AplicarFiltroLocal()
    {
        Proveedores.Clear();

        var filtrados = string.IsNullOrWhiteSpace(TextoBusqueda)
            ? _cacheProveedores
            : _cacheProveedores.Where(p =>
                p.RazonSocial.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                p.Cuit.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase) ||
                (p.Email != null && p.Email.Contains(TextoBusqueda, StringComparison.OrdinalIgnoreCase)));

        foreach (var proveedor in filtrados.OrderBy(p => p.RazonSocial))
        {
            Proveedores.Add(proveedor);
        }
    }
}
