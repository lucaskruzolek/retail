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
/// Sigue el patrón MVVM de CommunityToolkit con paginación integrada y delegación de diálogos a IProveedorDialogService.
/// </summary>
public partial class ProveedoresViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;
    private readonly IProveedorDialogService _dialogService;
    private readonly ILogger<ProveedoresViewModel> _logger;

    private List<ProveedorDto> _cacheProveedores = new();
    private List<ProveedorDto> _filtrados = new();

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();

    [ObservableProperty]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeEstado))]
    private string? _mensajeEstado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMensajeError))]
    private string? _mensajeError;

    public bool TieneMensajeEstado => !string.IsNullOrWhiteSpace(MensajeEstado);
    public bool TieneMensajeError => !string.IsNullOrWhiteSpace(MensajeError);

    [ObservableProperty]
    private int _totalProveedores;

    // ------------------------------------------------------------------ //
    // Paginación de Proveedores                                          //
    // ------------------------------------------------------------------ //

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPaginas))]
    [NotifyPropertyChangedFor(nameof(PuedeRetrocederPagina))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _paginaActual = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPaginas))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _tamanoPagina = 20;

    public IReadOnlyList<int> TamanosPaginaDisponibles { get; } = new[] { 10, 20, 50 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPaginas))]
    [NotifyPropertyChangedFor(nameof(PuedeAvanzarPagina))]
    [NotifyPropertyChangedFor(nameof(InformacionPaginacion))]
    private int _totalRegistrosFiltrados;

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling((double)TotalRegistrosFiltrados / Math.Max(1, TamanoPagina)));

    public bool PuedeRetrocederPagina => PaginaActual > 1;

    public bool PuedeAvanzarPagina => PaginaActual < TotalPaginas;

    public string InformacionPaginacion => TotalRegistrosFiltrados == 0
        ? "Sin proveedores"
        : $"Mostrando {(PaginaActual - 1) * TamanoPagina + 1} a {Math.Min(PaginaActual * TamanoPagina, TotalRegistrosFiltrados)} de {TotalRegistrosFiltrados} proveedores";

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
    // Notificaciones encadenadas                                         //
    // ------------------------------------------------------------------ //

    partial void OnProveedorSeleccionadoChanged(ProveedorDto? value)
    {
        OnPropertyChanged(nameof(HayProveedorSeleccionado));
    }

    partial void OnTextoBusquedaChanged(string value)
    {
        PaginaActual = 1;
        AplicarFiltroLocal();
    }

    partial void OnTamanoPaginaChanged(int value)
    {
        PaginaActual = 1;
        ActualizarPaginaActual();
    }

    // ------------------------------------------------------------------ //
    // Comandos de Carga y ABM                                             //
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
            TotalProveedores = _cacheProveedores.Count;
            PaginaActual = 1;
            AplicarFiltroLocal();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested ||
                                   ex.InnerException is OperationCanceledException ||
                                   ex.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Carga de proveedores cancelada por nueva acción del usuario.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar proveedores");
            MensajeError = "No se pudo cargar la lista de proveedores.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NuevoProveedorAsync()
    {
        var guardado = await _dialogService.AbrirFormularioNuevoProveedorAsync();
        if (guardado)
        {
            await CargarProveedoresAsync();
            MensajeEstado = "Proveedor registrado exitosamente.";
        }
    }

    [RelayCommand]
    private async Task EditarProveedorAsync(ProveedorDto? proveedor = null)
    {
        var target = proveedor ?? ProveedorSeleccionado;
        if (target is null)
        {
            MensajeError = "Seleccione un proveedor de la grilla para editar [F4].";
            return;
        }

        var guardado = await _dialogService.AbrirFormularioEditarProveedorAsync(target);
        if (guardado)
        {
            await CargarProveedoresAsync();
            MensajeEstado = "Proveedor actualizado exitosamente.";
        }
    }

    [RelayCommand]
    private async Task EliminarProveedorAsync(ProveedorDto? proveedor = null)
    {
        var target = proveedor ?? ProveedorSeleccionado;
        if (target is null)
        {
            MensajeError = "Seleccione un proveedor de la grilla para dar de baja.";
            return;
        }

        var confirmado = await _dialogService.ConfirmarEliminacionAsync(target.RazonSocial);
        if (!confirmado) return;

        try
        {
            var razonSocial = target.RazonSocial;
            await _proveedorService.BajaProveedorAsync(target.IdProveedor);
            if (ProveedorSeleccionado?.IdProveedor == target.IdProveedor)
            {
                ProveedorSeleccionado = null;
            }
            await CargarProveedoresAsync();
            MensajeEstado = $"Proveedor \"{razonSocial}\" dado de baja.";
        }
        catch (DomainException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al dar de baja proveedor");
            MensajeError = "Error al dar de baja el proveedor.";
        }
    }

    [RelayCommand]
    private async Task AbrirImportadorAsync(ProveedorDto? proveedor = null)
    {
        var target = proveedor ?? ProveedorSeleccionado;
        await _dialogService.AbrirImportadorCatalogosAsync(target);
    }

    // ------------------------------------------------------------------ //
    // Comandos de Paginación                                             //
    // ------------------------------------------------------------------ //

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
    public void PaginaAnterior()
    {
        if (PuedeRetrocederPagina)
        {
            PaginaActual--;
            ActualizarPaginaActual();
        }
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
    public void UltimaPagina()
    {
        if (PaginaActual != TotalPaginas)
        {
            PaginaActual = TotalPaginas;
            ActualizarPaginaActual();
        }
    }

    // ------------------------------------------------------------------ //
    // Métodos auxiliares de filtrado y paginación                        //
    // ------------------------------------------------------------------ //

    private void AplicarFiltroLocal()
    {
        if (string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            _filtrados = _cacheProveedores.ToList();
        }
        else
        {
            var termino = TextoBusqueda.Trim();
            _filtrados = _cacheProveedores
                .Where(p => p.RazonSocial.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                            p.Cuit.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                            (p.Email != null && p.Email.Contains(termino, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        TotalRegistrosFiltrados = _filtrados.Count;
        ActualizarPaginaActual();
    }

    private void ActualizarPaginaActual()
    {
        Proveedores.Clear();

        var itemsPagina = _filtrados
            .Skip((PaginaActual - 1) * TamanoPagina)
            .Take(TamanoPagina);

        foreach (var p in itemsPagina)
        {
            Proveedores.Add(p);
        }

        OnPropertyChanged(nameof(TotalPaginas));
        OnPropertyChanged(nameof(PuedeRetrocederPagina));
        OnPropertyChanged(nameof(PuedeAvanzarPagina));
        OnPropertyChanged(nameof(InformacionPaginacion));
    }
}
