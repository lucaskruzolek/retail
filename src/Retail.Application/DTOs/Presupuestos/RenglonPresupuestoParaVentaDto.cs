namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Renglón de un presupuesto recuperado para cobrarlo en el POS: trae lo necesario para armar la línea del ticket
/// (precio, stock y si es un servicio) a partir de la foto del presupuesto y del catálogo actual.
/// </summary>
public record class RenglonPresupuestoParaVentaDto
{
    public required int IdArticulo { get; init; }

    public string? CodigoBarras { get; init; }

    /// <summary>Descripción de la foto del presupuesto (PS-11).</summary>
    public required string Descripcion { get; init; }

    /// <summary>Cantidad presupuestada: tope de lo que se puede cobrar con el precio pactado.</summary>
    public required int Cantidad { get; init; }

    public required decimal PrecioPactado { get; init; }

    public required decimal PrecioActual { get; init; }

    /// <summary>El pactado si el presupuesto está vigente; el actual de catálogo si está vencido (PS-02, PS-03).</summary>
    public required decimal PrecioACobrar { get; init; }

    public required bool EsServicio { get; init; }

    public required int StockActual { get; init; }
}
