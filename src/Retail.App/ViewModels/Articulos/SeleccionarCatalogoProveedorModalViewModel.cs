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
/// Es el selector de "Agregar fuente" del formulario de artículo (Track 4.3, A-19): oculta los ítems que el
/// formulario ya tiene y deshabilita los que son fuente de otro artículo (A-1); el ítem que en la base sigue
/// siendo de este artículo, pero el usuario quitó sin guardar, se puede volver a elegir (M6).
/// </summary>
public partial class SeleccionarCatalogoProveedorModalViewModel : ObservableObject
{
    private readonly IProveedorService _proveedorService;
    private readonly TimeProvider _reloj;
    private ContextoSelectorFuente _contexto = new(0, null, null, []);

    public ObservableCollection<ProveedorDto> Proveedores { get; } = new();

    public ObservableCollection<ItemCatalogoSelectorFila> ItemsCatalogo { get; } = new();

    [ObservableProperty]
    private ProveedorDto? _proveedorSeleccionado;

    [ObservableProperty]
    private string _textoBusqueda = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemSeleccionado))]
    [NotifyPropertyChangedFor(nameof(HayItemSeleccionado))]
    private ItemCatalogoSelectorFila? _filaSeleccionada;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public bool DialogResult { get; private set; }

    /// <summary>
    /// Ítem elegido (el DTO que devuelve el diálogo); nulo si no hay fila elegida.
    /// </summary>
    public CatalogoProveedorDto? ItemSeleccionado => FilaSeleccionada?.Item;

    public bool HayItemSeleccionado => FilaSeleccionada is { EsSeleccionable: true };

    /// <summary>
    /// Caption de la tarjeta de arriba: "Artículo" si ya existe, "Artículo nuevo" en el alta.
    /// </summary>
    public string TituloArticulo => _contexto.IdArticulo > 0 ? "Artículo" : "Artículo nuevo";

    public string DescripcionArticulo =>
        string.IsNullOrWhiteSpace(_contexto.Descripcion) ? "(sin descripción todavía)" : _contexto.Descripcion.Trim();

    public string? CodigoBarrasArticulo =>
        string.IsNullOrWhiteSpace(_contexto.CodigoBarras) ? null : _contexto.CodigoBarras.Trim();

    public bool TieneCodigoBarrasArticulo => CodigoBarrasArticulo != null;

    public SeleccionarCatalogoProveedorModalViewModel(IProveedorService proveedorService, TimeProvider? reloj = null)
    {
        _proveedorService = proveedorService ?? throw new ArgumentNullException(nameof(proveedorService));
        _reloj = reloj ?? TimeProvider.System;
    }

    /// <summary>
    /// Prepara el selector para el artículo del formulario (Track 4.3, A-19) y hace la primera búsqueda.
    /// </summary>
    public async Task InicializarAsync(
        ContextoSelectorFuente contexto,
        string? textoInicial = null,
        int? idProveedorInicial = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        _contexto = contexto;
        OnPropertyChanged(nameof(TituloArticulo));
        OnPropertyChanged(nameof(DescripcionArticulo));
        OnPropertyChanged(nameof(CodigoBarrasArticulo));
        OnPropertyChanged(nameof(TieneCodigoBarrasArticulo));

        IsBusy = true;
        MensajeError = null;
        FilaSeleccionada = null;
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
            var idsEnFormulario = _contexto.IdsEnFormulario.ToHashSet();

            ItemsCatalogo.Clear();
            foreach (var item in resultado.Items.Where(i => !idsEnFormulario.Contains(i.Id)))
            {
                ItemsCatalogo.Add(new ItemCatalogoSelectorFila(item, EsSeleccionable(item), _reloj));
            }

            FilaSeleccionada = ItemsCatalogo.FirstOrDefault(f => f.EsSeleccionable);
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

    /// <summary>
    /// Libre, o vinculado a este mismo artículo (M6: el usuario lo quitó del formulario sin guardar). En el alta
    /// (id 0) un ítem vinculado nunca es elegible; el de otro artículo tampoco (A-1).
    /// </summary>
    private bool EsSeleccionable(CatalogoProveedorDto item)
    {
        return !item.EstaVinculado
            || (_contexto.IdArticulo > 0 && item.IdArticuloVinculado == _contexto.IdArticulo);
    }

    [RelayCommand]
    private void Seleccionar(Window? window)
    {
        if (FilaSeleccionada == null)
        {
            MensajeError = "Debe seleccionar un ítem de la lista.";
            return;
        }

        if (!FilaSeleccionada.EsSeleccionable)
        {
            MensajeError = $"Ese ítem no se puede agregar: {FilaSeleccionada.TextoEstado}.";
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
