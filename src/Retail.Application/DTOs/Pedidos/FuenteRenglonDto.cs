namespace Retail.Application.DTOs.Pedidos;

/// <summary>
/// Una de las fuentes de un artículo, tal como la ofrece el combo del modal de pedido (Track 4.3, A-18, K1): con qué
/// proveedor, a qué costo y con qué fecha de lista se puede pedir, y cuánto de ese ítem ya figura en el borrador de
/// su proveedor. Proyección de solo lectura (Ley 8).
/// </summary>
public record class FuenteRenglonDto
{
    public int IdCatalogoProveedor { get; init; }

    public int IdProveedor { get; init; }

    public string RazonSocialProveedor { get; init; } = string.Empty;

    public string CodigoProveedor { get; init; } = string.Empty;

    public string Descripcion { get; init; } = string.Empty;

    public decimal CostoActual { get; init; }

    /// <summary>
    /// Fecha de la última planilla que actualizó el ítem: una lista vieja y barata es la trampa que motivó A-2.
    /// </summary>
    public DateTime FechaActualizacion { get; init; }

    public bool EsPrincipal { get; init; }

    public int CantidadEnBorrador { get; init; }
}
