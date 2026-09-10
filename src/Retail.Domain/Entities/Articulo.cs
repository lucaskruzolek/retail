using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Raíz de Agregado que representa a un artículo comercial, producto artesanal o servicio en el catálogo.
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

        return Math.Round(costoReposicion * (1m + (porcentajeGanancia / 100m)), 2);
    }

    public void ActualizarCostoYRecalcularPrecio(decimal nuevoCosto)
    {
        CostoReposicion = nuevoCosto;
        PrecioVenta = CalcularPrecioVenta(CostoReposicion, PorcentajeGanancia);
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

        Descripcion = descripcion.Trim();
        IdCategoria = idCategoria;
        IdMarca = idMarca;
        CodigoBarras = string.IsNullOrWhiteSpace(codigoBarras) ? null : codigoBarras.Trim();
        CostoReposicion = costoReposicion;
        PorcentajeGanancia = porcentajeGanancia;
        PrecioVenta = CalcularPrecioVenta(costoReposicion, porcentajeGanancia);
        EsServicio = esServicio;
        StockActual = esServicio ? 0 : Math.Max(0, stockActual);
        StockMinimo = esServicio ? 0 : Math.Max(0, stockMinimo);
        IdCatalogoProveedor = idCatalogoProveedor;
    }
}
