using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Proveedores;
using Retail.Domain.Entities;

namespace Retail.App.ViewModels.Articulos;

/// <summary>
/// ViewModel para el diálogo modal de creación o edición de artículos y servicios del catálogo (RF-04).
/// Gestiona el cálculo reactivo del precio de venta según costo de reposición y margen de ganancia.
/// </summary>
public partial class ArticuloFormViewModel : ObservableObject
{
    [ObservableProperty]
    private int _idArticulo;

    [ObservableProperty]
    private string? _codigoBarras;

    [ObservableProperty]
    private string _descripcion = string.Empty;

    [ObservableProperty]
    private int? _idCategoria;

    [ObservableProperty]
    private int? _idMarca;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaVinculadoAProveedor))]
    [NotifyPropertyChangedFor(nameof(NoEstaVinculadoAProveedor))]
    [NotifyPropertyChangedFor(nameof(TextoBotonVinculacion))]
    private int? _idCatalogoProveedor;

    [ObservableProperty]
    private string? _proveedorRazonSocial;

    [ObservableProperty]
    private string? _codigoProveedor;

    [ObservableProperty]
    private string? _descripcionProveedor;

    [ObservableProperty]
    private decimal? _costoProveedor;

    public bool EstaVinculadoAProveedor => IdCatalogoProveedor.HasValue && IdCatalogoProveedor.Value > 0;

    public bool NoEstaVinculadoAProveedor => !EstaVinculadoAProveedor;

    public string TextoBotonVinculacion => EstaVinculadoAProveedor ? "Cambiar..." : "Vincular con Distribuidor...";

    public Func<string?, int?, Task<CatalogoProveedorDto?>>? OnAbrirSelectorProveedorAsync { get; set; }

    [ObservableProperty]
    private decimal _costoReposicion;

    [ObservableProperty]
    private decimal _porcentajeGanancia = 40m;

    [ObservableProperty]
    private decimal _precioVentaCalculado;

    [ObservableProperty]
    private int _stockActual;

    [ObservableProperty]
    private int _stockMinimo = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManejaStockFisico))]
    private bool _esServicio;

    public bool ManejaStockFisico => !EsServicio;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsAlta))]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    private bool _esModoEdicion;

    public bool EsAlta => !EsModoEdicion;

    public string TituloVentana => EsModoEdicion ? "Modificar Artículo de Catálogo" : "Alta de Artículo / Producto";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneError))]
    private string? _mensajeError;

    public bool TieneError => !string.IsNullOrWhiteSpace(MensajeError);

    public ObservableCollection<CategoriaDto> Categorias { get; } = new();

    public ObservableCollection<MarcaDto> Marcas { get; } = new();

    public Func<Task>? OnGuardarAsync { get; set; }

    public ArticuloFormViewModel()
    {
        RecalcularPrecioVenta();
    }

    partial void OnCostoReposicionChanged(decimal value)
    {
        RecalcularPrecioVenta();
    }

    partial void OnPorcentajeGananciaChanged(decimal value)
    {
        RecalcularPrecioVenta();
    }

    partial void OnEsServicioChanged(bool value)
    {
        if (value)
        {
            StockActual = 0;
            StockMinimo = 0;
        }
    }

    public void RecalcularPrecioVenta()
    {
        if (CostoReposicion < 0m || PorcentajeGanancia < 0m)
        {
            PrecioVentaCalculado = 0m;
            return;
        }

        PrecioVentaCalculado = Articulo.CalcularPrecioVenta(CostoReposicion, PorcentajeGanancia);
    }

    public void ConfigurarAlta(IReadOnlyList<CategoriaDto> categorias, IReadOnlyList<MarcaDto> marcas)
    {
        EsModoEdicion = false;
        IdArticulo = 0;
        CodigoBarras = null;
        Descripcion = string.Empty;
        CostoReposicion = 0m;
        PorcentajeGanancia = 40m;
        StockActual = 0;
        StockMinimo = 5;
        EsServicio = false;
        IdCatalogoProveedor = null;
        ProveedorRazonSocial = null;
        CodigoProveedor = null;
        DescripcionProveedor = null;
        CostoProveedor = null;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;

        CargarListas(categorias, marcas);
        RecalcularPrecioVenta();
    }

    public void ConfigurarEdicion(ArticuloDto articulo, IReadOnlyList<CategoriaDto> categorias, IReadOnlyList<MarcaDto> marcas)
    {
        ArgumentNullException.ThrowIfNull(articulo);

        EsModoEdicion = true;
        IdArticulo = articulo.IdArticulo;
        CodigoBarras = articulo.CodigoBarras;
        Descripcion = articulo.Descripcion;
        IdCategoria = articulo.IdCategoria ?? 0;
        IdMarca = articulo.IdMarca ?? 0;
        IdCatalogoProveedor = articulo.IdCatalogoProveedor;
        ProveedorRazonSocial = articulo.ProveedorNombre;
        CodigoProveedor = articulo.CodigoProveedor;
        DescripcionProveedor = articulo.DescripcionProveedor;
        CostoProveedor = articulo.CostoCatalogoProveedor;
        CostoReposicion = articulo.CostoReposicion;
        PorcentajeGanancia = articulo.PorcentajeGanancia;
        StockActual = articulo.StockActual;
        StockMinimo = articulo.StockMinimo;
        EsServicio = articulo.EsServicio;
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;

        CargarListas(categorias, marcas);
        RecalcularPrecioVenta();
    }

    private void CargarListas(IReadOnlyList<CategoriaDto> categorias, IReadOnlyList<MarcaDto> marcas)
    {
        Categorias.Clear();
        Categorias.Add(new CategoriaDto { IdCategoria = 0, NombreCategoria = "(Ninguna)" });
        foreach (var cat in categorias)
        {
            Categorias.Add(cat);
        }

        Marcas.Clear();
        Marcas.Add(new MarcaDto { IdMarca = 0, NombreMarca = "(Ninguno/a)" });
        foreach (var marca in marcas)
        {
            Marcas.Add(marca);
        }

        IdCategoria ??= 0;
        IdMarca ??= 0;
    }

    public bool Validar()
    {
        MensajeError = null;

        if (string.IsNullOrWhiteSpace(Descripcion) || Descripcion.Trim().Length < 2)
        {
            MensajeError = "La descripción del artículo debe contener al menos 2 caracteres.";
            return false;
        }

        if (Descripcion.Trim().Length > 200)
        {
            MensajeError = "La descripción del artículo no puede exceder 200 caracteres.";
            return false;
        }

        if (CostoReposicion < 0m)
        {
            MensajeError = "El costo de reposición no puede ser negativo.";
            return false;
        }

        if (PorcentajeGanancia < 0m)
        {
            MensajeError = "El porcentaje de ganancia no puede ser negativo.";
            return false;
        }

        if (!EsServicio)
        {
            if (StockActual < 0)
            {
                MensajeError = "El stock actual no puede ser negativo.";
                return false;
            }

            if (StockMinimo < 0)
            {
                MensajeError = "El stock mínimo no puede ser negativo.";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(CodigoBarras) && CodigoBarras.Trim().Length > 50)
        {
            MensajeError = "El código de barras no puede exceder 50 caracteres.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    public async Task VincularCatalogoAsync()
    {
        if (OnAbrirSelectorProveedorAsync == null)
        {
            return;
        }

        var termino = !string.IsNullOrWhiteSpace(CodigoBarras)
            ? CodigoBarras
            : Descripcion;

        var item = await OnAbrirSelectorProveedorAsync(termino, null);
        if (item != null)
        {
            IdCatalogoProveedor = item.Id;
            ProveedorRazonSocial = item.ProveedorRazonSocial;
            CodigoProveedor = item.CodigoProveedor;
            DescripcionProveedor = item.DescripcionProveedor;
            CostoProveedor = item.CostoReposicion;

            CostoReposicion = item.CostoReposicion;
            RecalcularPrecioVenta();

            if (string.IsNullOrWhiteSpace(Descripcion))
            {
                Descripcion = item.DescripcionProveedor;
            }

            if (string.IsNullOrWhiteSpace(CodigoBarras) && !string.IsNullOrWhiteSpace(item.CodigoBarras))
            {
                CodigoBarras = item.CodigoBarras;
            }
        }
    }

    [RelayCommand]
    public void DesvincularCatalogo()
    {
        IdCatalogoProveedor = null;
        ProveedorRazonSocial = null;
        CodigoProveedor = null;
        DescripcionProveedor = null;
        CostoProveedor = null;
    }

    public CrearArticuloDto ObtenerCrearDto() => new()
    {
        CodigoBarras = string.IsNullOrWhiteSpace(CodigoBarras) ? null : CodigoBarras.Trim(),
        Descripcion = Descripcion.Trim(),
        IdCategoria = (IdCategoria.HasValue && IdCategoria.Value > 0) ? IdCategoria.Value : null,
        IdMarca = (IdMarca.HasValue && IdMarca.Value > 0) ? IdMarca.Value : null,
        IdCatalogoProveedor = IdCatalogoProveedor,
        CostoReposicion = CostoReposicion,
        PorcentajeGanancia = PorcentajeGanancia,
        StockActual = EsServicio ? 0 : StockActual,
        StockMinimo = EsServicio ? 0 : StockMinimo,
        EsServicio = EsServicio
    };

    public ActualizarArticuloDto ObtenerActualizarDto() => new()
    {
        IdArticulo = IdArticulo,
        CodigoBarras = string.IsNullOrWhiteSpace(CodigoBarras) ? null : CodigoBarras.Trim(),
        Descripcion = Descripcion.Trim(),
        IdCategoria = (IdCategoria.HasValue && IdCategoria.Value > 0) ? IdCategoria.Value : null,
        IdMarca = (IdMarca.HasValue && IdMarca.Value > 0) ? IdMarca.Value : null,
        IdCatalogoProveedor = IdCatalogoProveedor,
        CostoReposicion = CostoReposicion,
        PorcentajeGanancia = PorcentajeGanancia,
        StockActual = EsServicio ? 0 : StockActual,
        StockMinimo = EsServicio ? 0 : StockMinimo,
        EsServicio = EsServicio
    };
}
