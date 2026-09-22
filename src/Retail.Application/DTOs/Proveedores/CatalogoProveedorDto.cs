using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Representa un ítem en el catálogo del proveedor y su estado de vinculación con la tienda.
/// </summary>
public record class CatalogoProveedorDto : BaseDto
{
    public required int Id { get; init; }

    public required int IdProveedor { get; init; }
    public string? ProveedorRazonSocial { get; init; }
    public required string CodigoProveedor { get; init; }

    public string? CodigoBarras { get; init; }

    public required string DescripcionProveedor { get; init; }

    public required decimal CostoReposicion { get; init; }

    public DateTime FechaActualizacion { get; init; }

    // Propiedades de vinculación y curaduría (RF-05)
    public bool EstaVinculado { get; init; }

    public bool CoincideConArticuloTienda { get; init; }

    public int? IdArticuloVinculado { get; init; }

    public string? NombreArticuloTienda { get; init; }

    public decimal? PrecioVentaTienda { get; init; }

    public int? StockActualTienda { get; init; }

    private bool _estaSeleccionado;

    /// <summary>
    /// Indica si el ítem está seleccionado para operaciones por lote (e.g. incorporación a tienda).
    /// </summary>
    public bool EstaSeleccionado
    {
        get => _estaSeleccionado;
        set
        {
            if (_estaSeleccionado != value)
            {
                _estaSeleccionado = value;
                OnPropertyChanged();
            }
        }
    }
}
