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
    [NotifyPropertyChangedFor(nameof(CodigoBarrasItem))]
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

    /// <summary>
    /// Si el ítem pasa a ser la fuente principal del artículo y fija su costo (Track 4.3, J4). Desmarcado, queda como
    /// fuente adicional; si el artículo no tiene principal, lo será de todos modos.
    /// </summary>
    [ObservableProperty]
    private bool _comoPrincipal;

    public ObservableCollection<ArticuloVentaDto> ArticulosEncontrados { get; } = new();

    public bool DialogResult { get; private set; }

    public int? IdArticuloSeleccionado => ArticuloSeleccionado?.IdArticulo;

    /// <summary>Código de barras del ítem del catálogo, para mostrarlo como dato informativo (no como texto de búsqueda).</summary>
    public string? CodigoBarrasItem => ItemCatalogo?.CodigoBarras;

    public VincularArticuloModalViewModel(IInventarioService inventarioService)
    {
        _inventarioService = inventarioService ?? throw new ArgumentNullException(nameof(inventarioService));
    }

    /// <summary>
    /// Prepara el diálogo para un ítem. El buscador arranca vacío (con el foco puesto en la vista) y no se consulta
    /// hasta que el usuario escribe: el código de barras del ítem se muestra como dato, no se vuelca al cuadro.
    /// </summary>
    public Task InicializarAsync(CatalogoProveedorDto item)
    {
        ItemCatalogo = item ?? throw new ArgumentNullException(nameof(item));
        TextoBusqueda = string.Empty;

        ArticulosEncontrados.Clear();
        ArticuloSeleccionado = null;
        MensajeError = null;
        ComoPrincipal = false;

        return Task.CompletedTask;
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
