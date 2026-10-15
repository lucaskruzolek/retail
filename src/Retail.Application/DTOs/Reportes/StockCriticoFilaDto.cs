using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>Un artículo con stock por debajo del mínimo.</summary>
public record class StockCriticoFilaDto : BaseDto
{
    public int IdArticulo { get; init; }

    public string Descripcion { get; init; } = string.Empty;

    public string? CodigoBarras { get; init; }

    public string? Categoria { get; init; }

    public string? Marca { get; init; }

    public int StockActual { get; init; }

    public int StockMinimo { get; init; }

    /// <summary>Unidades que faltan para llegar al mínimo.</summary>
    public int Faltante { get; init; }

    public decimal CostoReposicion { get; init; }

    public decimal PrecioVenta { get; init; }
}
