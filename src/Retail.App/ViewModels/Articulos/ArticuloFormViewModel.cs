using System.Collections.ObjectModel;
using System.Globalization;
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

    /// <summary>
    /// Fuentes del artículo ordenadas por costo, con la principal marcada (Track 4.3, A-19). Son el estado deseado:
    /// agregar, quitar o cambiar la principal no tiene efecto hasta Guardar, y Cancelar lo descarta todo.
    /// </summary>
    public ObservableCollection<FuenteFormularioFila> Fuentes { get; } = new();

    public bool TieneFuentes => Fuentes.Count > 0;

    public bool NoTieneFuentes => !TieneFuentes;

    /// <summary>
    /// La fuente principal elegida, o <c>null</c> si el artículo no tiene principal.
    /// </summary>
    public int? IdCatalogoProveedor => Fuentes.FirstOrDefault(f => f.EsPrincipal)?.IdCatalogoProveedor;

    /// <summary>
    /// Con fuente principal el costo no se edita a mano: al guardar lo fija siempre la principal. Sin principal, el
    /// costo del formulario se respeta y es editable.
    /// </summary>
    public bool TieneFuentePrincipal => IdCatalogoProveedor != null;

    public bool CostoEditable => !TieneFuentePrincipal;

    public bool CostoSoloLectura => TieneFuentePrincipal;

    public string TextoOrigenCosto => Fuentes.FirstOrDefault(f => f.EsPrincipal) is { } principal
        ? $"Fijado por la fuente principal: {principal.RazonSocialProveedor}"
        : string.Empty;

    /// <summary>
    /// Abre el selector de ítems de catálogo con el término inicial y los ítems que ya están en el formulario, que no
    /// se ofrecen. Devuelve el ítem elegido o <c>null</c> si se canceló.
    /// </summary>
    public Func<string?, IReadOnlyCollection<int>, Task<CatalogoProveedorDto?>>? OnAbrirSelectorProveedorAsync { get; set; }

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

    /// <summary>
    /// Elegir Servicio no borra el stock cargado: así, volver a Producto no lo pierde ni muestra un ajuste que nadie
    /// hizo. Al guardar un servicio, el DTO envía 0 y el dominio lo fuerza.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ManejaStockFisico))]
    [NotifyPropertyChangedFor(nameof(MostrarFuentes))]
    [NotifyPropertyChangedFor(nameof(PuedeAgregarFuentes))]
    [NotifyCanExecuteChangedFor(nameof(AgregarFuenteCommand))]
    private bool _esServicio;

    public bool ManejaStockFisico => !EsServicio;

    /// <summary>
    /// Un servicio no se compra a un proveedor, así que no tiene fuentes: con fuentes cargadas no se puede elegir
    /// Servicio hasta quitarlas.
    /// </summary>
    public bool PuedeSerServicio => !TieneFuentes;

    public string? AvisoNoPuedeSerServicio => TieneFuentes
        ? "Quite las fuentes para convertirlo en servicio: un servicio no se compra a un proveedor."
        : null;

    public bool PuedeAgregarFuentes => !EsServicio;

    /// <summary>
    /// La sección Fuentes no aplica a una presentación derivada ni a un servicio. Un servicio que ya tiene fuentes
    /// (datos anteriores a la regla) la sigue mostrando, para poder quitarlas y guardar.
    /// </summary>
    public bool MostrarFuentes => !EsDerivado && (!EsServicio || TieneFuentes);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsAlta))]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    [NotifyPropertyChangedFor(nameof(EtiquetaStockActual))]
    [NotifyPropertyChangedFor(nameof(StockActualEditable))]
    [NotifyPropertyChangedFor(nameof(MostrarStockSoloLectura))]
    [NotifyPropertyChangedFor(nameof(MostrarAccionesAjusteStock))]
    [NotifyPropertyChangedFor(nameof(TieneAjusteStock))]
    private bool _esModoEdicion;

    public bool EsAlta => !EsModoEdicion;

    /// <summary>
    /// Stock con el que se abrió el formulario (0 en el alta). Es la base para calcular la diferencia del ajuste.
    /// </summary>
    private int _stockActualOriginal;

    /// <summary>
    /// En edición el stock no se escribe directo: primero se pulsa "Ajustar". El registro de ajustes con motivo
    /// llegará con su propio módulo; el botón ya ocupa su lugar definitivo en el formulario.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StockActualEditable))]
    [NotifyPropertyChangedFor(nameof(MostrarStockSoloLectura))]
    [NotifyPropertyChangedFor(nameof(MostrarAccionesAjusteStock))]
    private bool _estaAjustandoStock;

    public bool StockActualEditable => EsAlta || EstaAjustandoStock;

    public bool MostrarStockSoloLectura => !StockActualEditable;

    public bool MostrarAccionesAjusteStock => EsModoEdicion && EstaAjustandoStock;

    public int DiferenciaStock => StockActual - _stockActualOriginal;

    public bool TieneAjusteStock => EsModoEdicion && DiferenciaStock != 0;

    public string TextoAjusteStock =>
        $"Ajuste: {_stockActualOriginal} → {StockActual} ({DiferenciaStock.ToString("+#;-#;0", CultureInfo.InvariantCulture)})";

    public string EtiquetaStockActual => EsAlta ? "Stock inicial" : "Stock actual";

    /// <summary>
    /// Habilita la edición del stock en un artículo existente.
    /// </summary>
    [RelayCommand]
    public void AjustarStock()
    {
        EstaAjustandoStock = true;
    }

    /// <summary>
    /// Cierra el modo de ajuste conservando el valor tipeado; se persiste recién al guardar el formulario.
    /// </summary>
    [RelayCommand]
    public void ConfirmarAjusteStock()
    {
        EstaAjustandoStock = false;
    }

    /// <summary>
    /// Cierra el modo de ajuste y restaura el stock con el que se abrió el formulario.
    /// </summary>
    [RelayCommand]
    public void DescartarAjusteStock()
    {
        StockActual = _stockActualOriginal;
        EstaAjustandoStock = false;
    }

    partial void OnStockActualChanged(int value)
    {
        NotificarAjusteStock();
    }

    private void NotificarAjusteStock()
    {
        OnPropertyChanged(nameof(DiferenciaStock));
        OnPropertyChanged(nameof(TieneAjusteStock));
        OnPropertyChanged(nameof(TextoAjusteStock));
    }

    public string TituloVentana => EsDerivado
        ? "Modificar Presentación de Venta"
        : EsModoEdicion ? "Modificar Artículo de Catálogo" : "Alta de Artículo / Producto";

    /// <summary>
    /// El artículo editado es una presentación derivada (RF-21): no puede ser servicio ni vincularse a un
    /// distribuidor, y su costo lo recalcula el origen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoEsDerivado))]
    [NotifyPropertyChangedFor(nameof(TituloVentana))]
    [NotifyPropertyChangedFor(nameof(AvisoCostoDerivado))]
    [NotifyPropertyChangedFor(nameof(MostrarFuentes))]
    private bool _esDerivado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvisoCostoDerivado))]
    private int? _unidadesPorOrigen;

    public bool NoEsDerivado => !EsDerivado;

    /// <summary>
    /// Aviso de la regla 4: el costo se puede editar, pero se reemplaza cuando cambia el costo del origen.
    /// </summary>
    public string AvisoCostoDerivado => EsDerivado
        ? $"Costo derivado del artículo de origen (÷ {UnidadesPorOrigen}). Si lo edita, se reemplazará la próxima vez que cambie el costo del origen."
        : string.Empty;

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

    private readonly TimeProvider _reloj;

    public ArticuloFormViewModel(TimeProvider? reloj = null)
    {
        _reloj = reloj ?? TimeProvider.System;
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
        _stockActualOriginal = 0;
        StockActual = 0;
        EstaAjustandoStock = false;
        NotificarAjusteStock();
        StockMinimo = 5;
        EsServicio = false;
        EsDerivado = false;
        UnidadesPorOrigen = null;
        CargarFuentes([]);
        MensajeError = null;
        IsBusy = false;
        OnGuardarAsync = null;

        CargarListas(categorias, marcas);
        RecalcularPrecioVenta();
    }

    /// <summary>
    /// Carga el artículo a editar con sus fuentes de la base (M5). Sin principal, el formulario muestra el costo
    /// guardado; con principal, muestra el costo de la principal, que es lo que fijará el guardado.
    /// </summary>
    public void ConfigurarEdicion(
        ArticuloDto articulo,
        IReadOnlyList<FuenteArticuloDto> fuentes,
        IReadOnlyList<CategoriaDto> categorias,
        IReadOnlyList<MarcaDto> marcas)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        ArgumentNullException.ThrowIfNull(fuentes);

        EsModoEdicion = true;
        IdArticulo = articulo.IdArticulo;
        CodigoBarras = articulo.CodigoBarras;
        Descripcion = articulo.Descripcion;
        IdCategoria = articulo.IdCategoria ?? 0;
        IdMarca = articulo.IdMarca ?? 0;
        CargarFuentes(fuentes);
        CostoReposicion = articulo.CostoReposicion;
        var principal = Fuentes.FirstOrDefault(f => f.EsPrincipal);
        if (principal != null)
        {
            CostoReposicion = principal.CostoActual;
        }

        PorcentajeGanancia = articulo.PorcentajeGanancia;
        _stockActualOriginal = articulo.StockActual;
        StockActual = articulo.StockActual;
        EstaAjustandoStock = false;
        NotificarAjusteStock();
        StockMinimo = articulo.StockMinimo;
        EsServicio = articulo.EsServicio;
        EsDerivado = articulo.EsDerivado;
        UnidadesPorOrigen = articulo.UnidadesPorOrigen;
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

    /// <summary>
    /// Agrega una fuente elegida en el selector, en su lugar según el costo. Si el artículo no tenía principal, la nueva
    /// lo es (M1). En el alta completa la descripción y el código de barras vacíos con los del ítem. Un servicio no
    /// agrega fuentes: el comando queda deshabilitado.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeAgregarFuentes))]
    public async Task AgregarFuenteAsync()
    {
        if (OnAbrirSelectorProveedorAsync == null)
        {
            return;
        }

        var termino = !string.IsNullOrWhiteSpace(CodigoBarras)
            ? CodigoBarras
            : Descripcion;
        var idsEnFormulario = Fuentes.Select(f => f.IdCatalogoProveedor).ToList();

        var item = await OnAbrirSelectorProveedorAsync(termino, idsEnFormulario);
        if (item == null || idsEnFormulario.Contains(item.Id))
        {
            return;
        }

        var fila = FuenteFormularioFila.DesdeItemDeCatalogo(item, _reloj);
        Fuentes.Insert(PosicionPorCosto(fila), fila);
        NotificarFuentes();

        if (IdCatalogoProveedor == null)
        {
            MarcarPrincipal(fila);
        }

        if (string.IsNullOrWhiteSpace(Descripcion))
        {
            Descripcion = item.DescripcionProveedor;
        }

        if (string.IsNullOrWhiteSpace(CodigoBarras) && !string.IsNullOrWhiteSpace(item.CodigoBarras))
        {
            CodigoBarras = item.CodigoBarras;
        }
    }

    /// <summary>
    /// Quita la fuente del estado deseado. Si era la principal, el artículo queda sin principal y conserva el costo (M3).
    /// </summary>
    [RelayCommand]
    public void QuitarFuente(FuenteFormularioFila? fila)
    {
        if (fila == null || !Fuentes.Remove(fila))
        {
            return;
        }

        NotificarFuentes();
    }

    /// <summary>
    /// Elige la principal y muestra su costo en el formulario como vista previa (M2); al guardar lo fija el dominio (A-2).
    /// </summary>
    [RelayCommand]
    public void MarcarPrincipal(FuenteFormularioFila? fila)
    {
        if (fila == null || fila.EsPrincipal || !Fuentes.Contains(fila))
        {
            return;
        }

        foreach (var fuente in Fuentes)
        {
            fuente.EsPrincipal = ReferenceEquals(fuente, fila);
        }

        CostoReposicion = fila.CostoActual;
        NotificarFuentes();
    }

    private void CargarFuentes(IReadOnlyList<FuenteArticuloDto> fuentes)
    {
        Fuentes.Clear();
        foreach (var fuente in fuentes.OrderBy(f => f.CostoActual).ThenBy(f => f.IdCatalogoProveedor))
        {
            Fuentes.Add(FuenteFormularioFila.DesdeFuente(fuente, _reloj));
        }

        NotificarFuentes();
    }

    private int PosicionPorCosto(FuenteFormularioFila nueva)
    {
        for (int i = 0; i < Fuentes.Count; i++)
        {
            var actual = Fuentes[i];
            if (actual.CostoActual > nueva.CostoActual
                || (actual.CostoActual == nueva.CostoActual && actual.IdCatalogoProveedor > nueva.IdCatalogoProveedor))
            {
                return i;
            }
        }

        return Fuentes.Count;
    }

    private void NotificarFuentes()
    {
        OnPropertyChanged(nameof(TieneFuentes));
        OnPropertyChanged(nameof(NoTieneFuentes));
        OnPropertyChanged(nameof(IdCatalogoProveedor));
        OnPropertyChanged(nameof(TieneFuentePrincipal));
        OnPropertyChanged(nameof(CostoEditable));
        OnPropertyChanged(nameof(CostoSoloLectura));
        OnPropertyChanged(nameof(TextoOrigenCosto));
        OnPropertyChanged(nameof(PuedeSerServicio));
        OnPropertyChanged(nameof(AvisoNoPuedeSerServicio));
        OnPropertyChanged(nameof(MostrarFuentes));
    }

    public CrearArticuloDto ObtenerCrearDto() => new()
    {
        CodigoBarras = string.IsNullOrWhiteSpace(CodigoBarras) ? null : CodigoBarras.Trim(),
        Descripcion = Descripcion.Trim(),
        IdCategoria = (IdCategoria.HasValue && IdCategoria.Value > 0) ? IdCategoria.Value : null,
        IdMarca = (IdMarca.HasValue && IdMarca.Value > 0) ? IdMarca.Value : null,
        IdCatalogoProveedor = IdCatalogoProveedor,
        IdsFuentes = Fuentes.Select(f => f.IdCatalogoProveedor).ToList(),
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
        IdsFuentes = Fuentes.Select(f => f.IdCatalogoProveedor).ToList(),
        CostoReposicion = CostoReposicion,
        PorcentajeGanancia = PorcentajeGanancia,
        StockActual = EsServicio ? 0 : StockActual,
        StockMinimo = EsServicio ? 0 : StockMinimo,
        EsServicio = EsServicio
    };
}
