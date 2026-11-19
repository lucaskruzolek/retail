namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Renglón de una lista con el precio del catálogo de hoy (L-08). Si el artículo fue dado de baja
/// (<see cref="EstaDisponible"/> en <c>false</c>) el precio y el subtotal son solo informativos y no suman al total
/// de la lista (L-13).
/// </summary>
public record class RenglonListaDto
{
    public required int IdArticulo { get; init; }

    public string? CodigoBarras { get; init; }

    public required string Descripcion { get; init; }

    public required int Cantidad { get; init; }

    public required decimal PrecioActual { get; init; }

    public required decimal Subtotal { get; init; }

    public required bool EstaDisponible { get; init; }
}
