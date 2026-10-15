using Retail.Application.DTOs.Common;

namespace Retail.Application.DTOs.Reportes;

/// <summary>Ventas de un día, semana o mes locales (un punto del gráfico de evolución).</summary>
public record class PuntoVentasDto : BaseDto
{
    public DateOnly InicioPeriodo { get; init; }

    public int CantidadVentas { get; init; }

    public decimal TotalVendido { get; init; }

    public decimal Descuentos { get; init; }
}
