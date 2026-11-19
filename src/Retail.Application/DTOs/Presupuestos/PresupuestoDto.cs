using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Presupuesto con sus renglones: lo que muestra el detalle, lo que se imprime y lo que se exporta a PDF.
/// </summary>
public record class PresupuestoDto
{
    public required int IdPresupuesto { get; init; }
    public required int IdUsuario { get; init; }
    public string? UsuarioNombre { get; init; }
    public int? IdCliente { get; init; }

    /// <summary><c>null</c> es Consumidor Final.</summary>
    public string? ClienteNombre { get; init; }

    /// <summary>Nota interna (PS-20): se muestra solo dentro del sistema, nunca en el ticket ni en el PDF.</summary>
    public string? Titulo { get; init; }

    /// <summary>Instante de emisión, en UTC.</summary>
    public required DateTime FechaEmision { get; init; }

    /// <summary>Último día de validez, inclusive (PS-09).</summary>
    public required DateOnly FechaVencimiento { get; init; }

    public required decimal Subtotal { get; init; }
    public required decimal Descuento { get; init; }
    public required decimal Total { get; init; }
    public required EstadoPresupuestoEnum Estado { get; init; }

    /// <summary>La vigencia la calcula el servidor con su reloj (PS-08); el DTO no lee la hora.</summary>
    public required bool EstaVigente { get; init; }

    public required IReadOnlyList<DetallePresupuestoDto> Items { get; init; }
}
