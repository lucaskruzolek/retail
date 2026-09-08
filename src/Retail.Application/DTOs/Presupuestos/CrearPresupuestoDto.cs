namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Parámetros para la confección de una cotización independiente. No afecta stock ni caja.
/// </summary>
public record class CrearPresupuestoDto
{
    public required int IdUsuario { get; init; }
    public int? IdCliente { get; init; }
    public decimal Descuento { get; init; } = 0;
    public required IReadOnlyList<DetallePresupuestoDto> Items { get; init; }
}
