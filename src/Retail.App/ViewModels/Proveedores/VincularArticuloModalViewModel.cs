using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Services;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el diálogo modal de vinculación asistida de un ítem mayorista a un artículo de la tienda (RF-05).
/// </summary>
public partial class VincularArticuloModalViewModel : ObservableObject
{
    private readonly IInventarioService _inventarioService;

    [ObservableProperty]
    private CatalogoProveedorDto? _itemCatalogo;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    private ArticuloVentaDto? _articuloSeleccionado;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public ObservableCollection<ArticuloVentaDto> ArticulosEncontrados { get; } = new();

    public bool DialogResult { get; private set; }

    public int? IdArticuloSeleccionado => ArticuloSeleccionado?.IdArticulo;

    public VincularArticuloModalViewModel(IInventarioService inventarioService)
    {
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
    }

    public async Task InicializarAsync(CatalogoProveedorDto item)
    {
        ItemCatalogo = item ?? throw new ArgumentNullException(nameof(item));
        TextoBusqueda = !string.IsNullOrWhiteSpace(item.CodigoBarras)
            ? item.CodigoBarras
            : item.DescripcionProveedor;

        ArticuloSeleccionado = null;
        MensajeError = null;

        await BuscarArticulosAsync();
    }

    [RelayCommand]
    public async Task BuscarArticulosAsync()
    {
        IsBusy = true;
        MensajeError = null;

        try
        {
            var resultados = await _inventarioService.BuscarArticulosParaVentaAsync(TextoBusqueda);
            ArticulosEncontrados.Clear();
            foreach (var art in resultados)
            {
                ArticulosEncontrados.Add(art);
            }

            if (ArticulosEncontrados.Count > 0)
            {
                ArticuloSeleccionado = ArticulosEncontrados[0];
            }
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al buscar artículos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Aceptar(Window window)
    {
        if (ArticuloSeleccionado is null)
        {
            MensajeError = "Debe seleccionar un artículo de la lista para vincular.";
            return;
        }

        MensajeError = null;
        DialogResult = true;
        window.DialogResult = true;
        window.Close();
    }

    [RelayCommand]
    private void Cancelar(Window window)
    {
        DialogResult = false;
        window.DialogResult = false;
        window.Close();
    }
}
