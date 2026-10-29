namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Ítem que se puede pedir, con los datos que el modal de pedido muestra antes de confirmar (A-17): el costo actual
/// del catálogo, el stock del artículo y la cantidad que ya figura en el borrador de su proveedor (al agregar, se
/// suma a esa cantidad). Los datos del ítem son los de la fuente por defecto (K2, K6); <see cref="Fuentes"/> trae
/// todas las del artículo para que el combo cambie de fuente sin volver a la base (Track 4.3, K1).
/// </summary>
public record class RenglonVistaPreviaDto
{
    public int IdCatalogoProveedor { get; init; }

    public int IdProveedor { get; init; }

    public string CodigoProveedor { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public decimal CostoActual { get; init; }

    public int IdArticulo { get; init; }

    public int StockActual { get; init; }

    public int StockMinimo { get; init; }

    public int CantidadEnBorrador { get; init; }

    /// <summary>
    /// Todas las fuentes vigentes del artículo, incluida la de este renglón, ordenadas por costo (A-18).
    /// </summary>
    public IReadOnlyList<FuenteRenglonDto> Fuentes { get; init; } = Array.Empty<FuenteRenglonDto>();
}
