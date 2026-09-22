using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;
using Retail.Domain.Enums;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel para el diálogo modal de búsqueda y selección de ítems de catálogo mayorista (Vía B, RF-05).
/// Permite explorar distribuidores y seleccionar un ítem para vincular a un artículo propio.
/// </summary>
public partial class SeleccionarCatalogoProveedorModalViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();

    public ObservableCollection<CatalogoProveedorDto> ItemsCatalogo { get; } = new();

    [ObservableProperty]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private CatalogoProveedorDto? _itemSeleccionado;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public bool DialogResult { get; private set; }

    public bool HayItemSeleccionado => ItemSeleccionado != null;

    public SeleccionarCatalogoProveedorModalViewModel(IProveedorService proveedorService)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
    }

    partial void OnItemSeleccionadoChanged(CatalogoProveedorDto? value)
    {
        OnPropertyChanged(nameof(HayItemSeleccionado));
    }

    public async Task InicializarAsync(string? textoInicial = null, int? idProveedorInicial = null)
    {
        IsBusy = true;
        MensajeError = null;
        ItemSeleccionado = null;
        DialogResult = false;
        TextoBusqueda = textoInicial?.Trim() ?? string.Empty;

        try
        {
            Proveedores.Clear();
            var todosProveedores = new ProveedorDto
            {
                IdProveedor = 0,
                RazonSocial = "(Todos los Distribuidores)",
                Cuit = string.Empty
            };
            Proveedores.Add(todosProveedores);

            var lista = await _proveedorService.ListarProveedoresAsync();
            foreach (var p in lista)
            {
                Proveedores.Add(p);
            }

            if (idProveedorInicial.HasValue && idProveedorInicial.Value > 0)
            {
                ProveedorSeleccionado = Proveedores.FirstOrDefault(p => p.IdProveedor == idProveedorInicial.Value) ?? todosProveedores;
            }
            else
            {
                ProveedorSeleccionado = todosProveedores;
            }

            await BuscarAsync();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al cargar proveedores: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task BuscarAsync()
    {
        IsBusy = true;
        MensajeError = null;

        try
        {
            var consulta = new ConsultaCatalogoProveedorDto
            {
                IdProveedor = ProveedorSeleccionado?.IdProveedor > 0 ? ProveedorSeleccionado.IdProveedor : null,
                TerminoBusqueda = string.IsNullOrWhiteSpace(TextoBusqueda) ? null : TextoBusqueda.Trim(),
                EstadoVinculacion = EstadoVinculacionCatalogoEnum.Todos,
                Pagina = 1,
                TamañoPagina = 50
            };

            var resultado = await _proveedorService.ListarItemsCatalogoAsync(consulta);
            ItemsCatalogo.Clear();
            foreach (var item in resultado.Items)
            {
                ItemsCatalogo.Add(item);
            }

            if (ItemsCatalogo.Count > 0)
            {
                ItemSeleccionado = ItemsCatalogo[0];
            }
            else
            {
                ItemSeleccionado = null;
            }
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al buscar en catálogos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Seleccionar(Window? window)
    {
        if (ItemSeleccionado == null)
        {
            MensajeError = "Debe seleccionar un ítem de la lista.";
            return;
        }

        DialogResult = true;
        if (window != null)
        {
            window.DialogResult = true;
            window.Close();
        }
    }

    [RelayCommand]
    private void Cancelar(Window? window)
    {
        DialogResult = false;
        if (window != null)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
