namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Representa un ítem en el catálogo del proveedor y su estado de vinculación con la tienda.
/// </summary>
public record class CatalogoProveedorDto
{
    public required int Id { get; init; }
    
    public required int IdProveedor { get; init; }
    
    public required string CodigoProveedor { get; init; }
    
    public required string DescripcionProveedor { get; init; }
    
    public required decimal CostoReposicion { get; init; }
    
    public DateTime FechaActualizacion { get; init; }
    
    // Propiedades de vinculación
    public bool EstaVinculado { get; init; }
    
    public int? IdArticuloVinculado { get; init; }
    
    public string? NombreArticuloTienda { get; init; }
    
    public decimal? PrecioVentaTienda { get; init; }
    
    public int? StockActualTienda { get; init; }
}
