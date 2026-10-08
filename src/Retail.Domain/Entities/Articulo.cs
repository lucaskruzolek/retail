using Retail.Domain.Common;
using Retail.Domain.Exceptions;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un artículo comercial, producto artesanal o servicio en el catálogo.
/// Un artículo puede ser una presentación derivada de otro (RF-21): por ejemplo, la unidad suelta de un pack.
/// </summary>
public class Articulo : BaseEntity, IAggregateRoot
{
    public string? CodigoBarras { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public int? IdCategoria { get; set; }

    public Categoria? Categoria { get; set; }

    public int? IdMarca { get; set; }

    public Marca? Marca { get; set; }

    public int? IdCatalogoProveedor { get; set; }

    public CatalogoProveedor? CatalogoProveedor { get; set; }

    /// <summary>
    /// Artículo de compra del que deriva esta presentación (RF-21). Nulo si no es un derivado.
    /// </summary>
    public int? IdArticuloOrigen { get; set; }

    public Articulo? ArticuloOrigen { get; set; }

    /// <summary>
    /// Unidades de esta presentación que contiene cada unidad del origen (entero mayor o igual a 1).
    /// </summary>
    public int? UnidadesPorOrigen { get; set; }

    public ICollection<Articulo> Presentaciones { get; set; } = new List<Articulo>();

    public decimal CostoReposicion { get; set; }

    public decimal PorcentajeGanancia { get; set; }

    public decimal PrecioVenta { get; set; }

    public int StockActual { get; set; }

    public int StockMinimo { get; set; }

    public bool EsServicio { get; set; }

    public ICollection<DetalleVenta> DetallesVentas { get; set; } = new List<DetalleVenta>();

    public ICollection<DetallePresupuesto> DetallesPresupuestos { get; set; } = new List<DetallePresupuesto>();

    public ICollection<DetalleCompra> DetallesCompras { get; set; } = new List<DetalleCompra>();

    public bool TieneStockBajo => !EsServicio && StockActual <= StockMinimo;

    public bool EsDerivado => IdArticuloOrigen.HasValue;

    public static decimal CalcularPrecioVenta(decimal costoReposicion, decimal porcentajeGanancia)
    {
        if (costoReposicion < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(costoReposicion), "El costo de reposición no puede ser negativo.");
        }

        if (porcentajeGanancia < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(porcentajeGanancia), "El porcentaje de ganancia no puede ser negativo.");
        }

        return RedondearMoneda(costoReposicion * (1m + (porcentajeGanancia / 100m)));
    }

    /// <summary>
    /// Costo de reposición de una presentación derivada: costo del origen dividido por las unidades que contiene (RF-21).
    /// </summary>
    public static decimal CalcularCostoPresentacion(decimal costoOrigen, int unidadesPorOrigen)
    {
        if (costoOrigen < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(costoOrigen), "El costo del artículo de origen no puede ser negativo.");
        }

        if (unidadesPorOrigen < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesPorOrigen), "Las unidades por origen deben ser al menos 1.");
        }

        return RedondearMoneda(costoOrigen / unidadesPorOrigen);
    }

    public void ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)
    {
        // Se calcula antes de asignar: si el cálculo falla, el artículo no queda con un costo nuevo y un precio viejo (H-08).
        var nuevoPrecio = CalcularPrecioVenta(nuevoCosto, PorcentajeGanancia);

        CostoReposicion = nuevoCosto;
        PrecioVenta = nuevoPrecio;
    }

    public void ActualizarDatos(
        string descripcion,
        int? idCategoria,
        int? idMarca,
        string? codigoBarras,
        decimal costoReposicion,
        decimal porcentajeGanancia,
        int stockActual,
        int stockMinimo,
        bool esServicio,
        int? idCatalogoProveedor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descripcion);

        if (idCategoria.HasValue && idCategoria.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCategoria), "La categoría debe ser válida.");
        }

        if (idMarca.HasValue && idMarca.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idMarca), "La marca debe ser válida.");
        }

        if (EsDerivado && idCatalogoProveedor.HasValue)
        {
            throw new DomainException("Una presentación derivada no puede vincularse a un catálogo de proveedor: solo se vincula su artículo de origen.");
        }

        if (EsDerivado && esServicio)
        {
            throw new DomainException("Una presentación derivada no puede ser un servicio.");
        }

        // Todas las validaciones y el cálculo ocurren antes de asignar el primer campo (H-08).
        var precioVenta = CalcularPrecioVenta(costoReposicion, porcentajeGanancia);

        Descripcion = descripcion.Trim();
        IdCategoria = idCategoria;
        IdMarca = idMarca;
        CodigoBarras = string.IsNullOrWhiteSpace(codigoBarras) ? null : codigoBarras.Trim();
        CostoReposicion = costoReposicion;
        PorcentajeGanancia = porcentajeGanancia;
        PrecioVenta = precioVenta;
        EsServicio = esServicio;
        StockActual = esServicio ? 0 : Math.Max(0, stockActual);
        StockMinimo = esServicio ? 0 : Math.Max(0, stockMinimo);
        IdCatalogoProveedor = idCatalogoProveedor;
    }

    public void VincularCatalogoProveedor(int idCatalogoProveedor, decimal nuevoCosto)
    {
        if (idCatalogoProveedor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(idCatalogoProveedor), "El identificador de catálogo de proveedor debe ser mayor a cero.");
        }

        if (EsDerivado)
        {
            throw new DomainException("Una presentación derivada no puede vincularse a un catálogo de proveedor: solo se vincula su artículo de origen.");
        }

        ActualizarCostoYRecalcularPrecio(nuevoCosto);
        IdCatalogoProveedor = idCatalogoProveedor;
    }

    public void DesvincularCatalogoProveedor()
    {
        IdCatalogoProveedor = null;
        CatalogoProveedor = null;
    }

    /// <summary>
    /// Convierte este artículo en una presentación derivada del origen indicado y toma de él su costo (RF-21).
    /// Las reglas que miran a otros artículos (por ejemplo, que el origen no se dé de baja) viven en Application.
    /// </summary>
    public void DefinirComoPresentacionDe(Articulo origen, int unidadesPorOrigen)
    {
        ArgumentNullException.ThrowIfNull(origen);

        if (unidadesPorOrigen < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesPorOrigen), "Las unidades por origen deben ser al menos 1.");
        }

        if (ReferenceEquals(origen, this) || (Id > 0 && origen.Id == Id))
        {
            throw new DomainException("Un artículo no puede ser presentación derivada de sí mismo.");
        }

        if (origen.EsDerivado)
        {
            throw new DomainException("El artículo de origen ya es una presentación derivada: la derivación es de un solo nivel.");
        }

        if (IdCatalogoProveedor.HasValue)
        {
            throw new DomainException("Un artículo vinculado a un catálogo de proveedor no puede ser una presentación derivada.");
        }

        if (EsServicio || origen.EsServicio)
        {
            throw new DomainException("Ni la presentación derivada ni su artículo de origen pueden ser servicios.");
        }

        if (Presentaciones.Count > 0)
        {
            throw new DomainException("Un artículo que ya es origen de otras presentaciones no puede ser, a su vez, una presentación derivada.");
        }

        var costoDerivado = CalcularCostoPresentacion(origen.CostoReposicion, unidadesPorOrigen);
        ActualizarCostoYRecalcularPrecio(costoDerivado);

        IdArticuloOrigen = origen.Id;
        ArticuloOrigen = origen;
        UnidadesPorOrigen = unidadesPorOrigen;
    }

    /// <summary>
    /// Recalcula el costo de la presentación a partir del nuevo costo del origen y su precio con el markup propio.
    /// Pisa un costo editado a mano: es el comportamiento acordado (RF-21).
    /// </summary>
    public void RecalcularCostoDesdeOrigen(decimal costoOrigen)
    {
        if (!EsDerivado || UnidadesPorOrigen is not int unidadesPorOrigen)
        {
            throw new InvalidOperationException("Solo una presentación derivada puede recalcular su costo desde un artículo de origen.");
        }

        ActualizarCostoYRecalcularPrecio(CalcularCostoPresentacion(costoOrigen, unidadesPorOrigen));
    }

    public void DescontarStock(int cantidad)
    {
        ValidarMovimientoDeStock(cantidad);

        if (cantidad > StockActual)
        {
            throw new StockInsuficienteException(Id, StockActual, cantidad);
        }

        StockActual -= cantidad;
    }

    public void IncrementarStock(int cantidad)
    {
        ValidarMovimientoDeStock(cantidad);

        StockActual = checked(StockActual + cantidad);
    }

    private void ValidarMovimientoDeStock(int cantidad)
    {
        if (cantidad < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser al menos 1.");
        }

        if (EsServicio)
        {
            throw new DomainException("Un servicio no maneja stock.");
        }
    }

    /// <summary>
    /// Único punto de redondeo monetario del artículo: 2 decimales, alejándose del cero en el punto medio (D-06).
    /// </summary>
    private static decimal RedondearMoneda(decimal importe)
    {
        return Math.Round(importe, 2, MidpointRounding.AwayFromZero);
    }
}
