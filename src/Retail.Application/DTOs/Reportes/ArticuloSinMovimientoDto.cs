using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>Un artículo con stock que no se vende hace tiempo.</summary>
public record class ArticuloSinMovimientoDto : BaseDto
{
    public int IdArticulo { get; init; }

    public string Descripcion { get; init; } = string.Empty;

    public string? CodigoBarras { get; init; }

    public string? Categoria { get; init; }

    public string? Marca { get; init; }

    public int StockActual { get; init; }

    public decimal CostoReposicion { get; init; }

    /// <summary>Última venta en hora local; <c>null</c> si nunca se vendió.</summary>
    public DateTime? UltimaVenta { get; init; }

    /// <summary>Stock actual por costo de reposición, redondeado.</summary>
    public decimal CapitalInmovilizado { get; init; }
}
