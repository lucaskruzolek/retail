using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>Un artículo del ranking de ventas, con el ingreso neto ya redondeado.</summary>
public record class RankingArticuloDto : BaseDto
{
    public int IdArticulo { get; init; }

    public string Descripcion { get; init; } = string.Empty;

    public string? CodigoBarras { get; init; }

    public string? Categoria { get; init; }

    public string? Marca { get; init; }

    public int Unidades { get; init; }

    public decimal IngresoNeto { get; init; }
}
