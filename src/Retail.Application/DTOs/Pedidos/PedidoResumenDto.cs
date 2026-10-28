using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Fila de la grilla de pedidos: proyección de solo lectura sin los renglones (RF-28, Ley 8).
/// </summary>
public record class PedidoResumenDto
{
    public int IdPedido { get; init; }

    public int IdProveedor { get; init; }

    public string RazonSocialProveedor { get; init; } = string.Empty;

    public EstadoPedidoEnum Estado { get; init; }

    public DateTime FechaCreacion { get; init; }

    public DateTime? FechaEmision { get; init; }

    public DateTime? FechaCierre { get; init; }

    public int CantidadRenglones { get; init; }

    public decimal TotalEstimado { get; init; }
}
