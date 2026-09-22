using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;

namespace Retail.App.ViewModels.Proveedores;

/// <summary>
/// ViewModel para el diálogo modal de confirmación y curaduría de incorporación de artículos a la tienda (Etapa 2.2, RF-05).
/// Permite definir un margen de ganancia global con aplicación masiva, ajustar márgenes individuales por producto con
/// cálculo reactivo del precio final de venta, y asociar opcionalmente categoría y marca destino.
/// </summary>
public partial class IncorporarArticulosModalViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _porcentajeGananciaGlobal = 40m;

    public decimal PorcentajeGanancia
    {
        get => PorcentajeGananciaGlobal;
        set => PorcentajeGananciaGlobal = value;
    }

    [ObservableProperty]
    private int? _idCategoriaDestino;

    [ObservableProperty]
    private int? _idMarcaDestino;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public ObservableCollection<ItemIncorporarArticuloViewModel> Items { get; } = new();

    public ObservableCollection<CategoriaDto> CategoriasDisponibles { get; } = new();

    public ObservableCollection<MarcaDto> MarcasDisponibles { get; } = new();

    public int TotalArticulos => Items.Count;

    public bool DialogResult { get; private set; }

    public void Inicializar(
        IEnumerable<CategoriaDto> categorias,
        IEnumerable<MarcaDto> marcas,
        IEnumerable<CatalogoProveedorDto>? items = null)
    {
        CategoriasDisponibles.Clear();
        foreach (var c in categorias)
        {
            CategoriasDisponibles.Add(c);
        }

        MarcasDisponibles.Clear();
        foreach (var m in marcas)
        {
            MarcasDisponibles.Add(m);
        }

        Items.Clear();
        if (items != null)
        {
            foreach (var item in items)
            {
                Items.Add(new ItemIncorporarArticuloViewModel(
                    item.Id,
                    item.CodigoProveedor,
                    item.CodigoBarras,
                    item.DescripcionProveedor,
                    item.CostoReposicion,
                    PorcentajeGananciaGlobal));
            }
        }

        IdCategoriaDestino = null;
        IdMarcaDestino = null;
        MensajeError = null;
        DialogResult = false;
        OnPropertyChanged(nameof(TotalArticulos));
    }

    [RelayCommand]
    public void AplicarGananciaGlobal()
    {
        if (PorcentajeGananciaGlobal < 0m)
        {
            MensajeError = "El porcentaje de ganancia no puede ser negativo.";
            return;
        }

        MensajeError = null;
        foreach (var item in Items)
        {
            item.PorcentajeGanancia = PorcentajeGananciaGlobal;
        }
    }

    [RelayCommand]
    private void Aceptar(Window window)
    {
        if (PorcentajeGananciaGlobal < 0m)
        {
            MensajeError = "El porcentaje de ganancia no puede ser negativo.";
            return;
        }

        if (Items.Any(i => i.PorcentajeGanancia < 0m))
        {
            MensajeError = "Ningún artículo puede tener un porcentaje de ganancia negativo.";
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

    public IncorporarCatalogoArticulosDto ObtenerDto()
    {
        var itemsDto = Items.Select(i => new ItemIncorporacionArticuloDto
        {
            IdCatalogo = i.IdCatalogo,
            PorcentajeGanancia = i.PorcentajeGanancia
        }).ToList();

        return new IncorporarCatalogoArticulosDto
        {
            Items = itemsDto,
            IdsCatalogo = Items.Select(i => i.IdCatalogo).ToList(),
            IdCategoria = IdCategoriaDestino,
            IdMarca = IdMarcaDestino,
            PorcentajeGananciaSugerido = PorcentajeGananciaGlobal
        };
    }
}
