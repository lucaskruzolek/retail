using Retail.Domain.Common;

namespace Retail.Domain.Entities;

/// <summary>
/// Ítem adquirido dentro de una compra de proveedor con su costo unitario de reposición.
/// Solo puede instanciarse a través de <see cref="Compra.AgregarDetalle"/> (Ley 2: Prohibición de repositorios internos).
/// </summary>
public class DetalleCompra : BaseEntity
{
    /// <summary>
    /// Constructor interno: garantiza que <see cref="DetalleCompra"/> solo se cree
    /// desde la raíz de su agregado (<see cref="Compra"/>).
    /// EF Core lo puede usar desde el mismo ensamblado a través de la configuración Fluent API.
    /// </summary>
    internal DetalleCompra(int idArticulo, int cantidad, decimal costoUnitario, decimal subtotalItem)
    {
        IdArticulo = idArticulo;
        Cantidad = cantidad;
        CostoUnitario = costoUnitario;
        SubtotalItem = subtotalItem;
    }

    // Constructor sin parámetros requerido por EF Core para la materialización desde la base de datos.
#pragma warning disable CS8618
    private DetalleCompra() { }
#pragma warning restore CS8618

    public int IdCompra { get; private set; }

    public Compra? Compra { get; private set; }

    public int IdArticulo { get; private set; }

    public Articulo? Articulo { get; private set; }

    public int Cantidad { get; private set; }

    public decimal CostoUnitario { get; private set; }

    public decimal SubtotalItem { get; private set; }
}
