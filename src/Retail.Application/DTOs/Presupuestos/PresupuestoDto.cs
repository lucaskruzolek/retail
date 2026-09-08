using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Información resumida de una cotización comercial.
/// </summary>
public record class PresupuestoDto
{
    public required int IdPresupuesto { get; init; }
    public required int IdUsuario { get; init; }
    public string? UsuarioNombre { get; init; }
    public int? IdCliente { get; init; }
    public string? ClienteNombre { get; init; }
    public required DateTime FechaEmision { get; init; }
    public required DateTime FechaVencimiento { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Descuento { get; init; }
    public required decimal Total { get; init; }
    public required EstadoPresupuestoEnum Estado { get; init; }
    public bool EstaVencido => DateTime.UtcNow > FechaVencimiento;
    public required IReadOnlyList<DetallePresupuestoDto> Items { get; init; }
}
