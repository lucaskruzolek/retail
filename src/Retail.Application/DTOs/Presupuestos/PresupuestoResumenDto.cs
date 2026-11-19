using Retail.Domain.Enums;

namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Fila de la grilla de presupuestos: proyección de solo lectura sin los renglones (Ley 8).
/// </summary>
public record class PresupuestoResumenDto
{
    public int IdPresupuesto { get; init; }

    /// <summary>Nota interna (PS-20).</summary>
    public string? Titulo { get; init; }

    public DateTime FechaEmision { get; init; }

    public DateOnly FechaVencimiento { get; init; }

    /// <summary><c>null</c> es Consumidor Final.</summary>
    public string? ClienteNombre { get; init; }

    public string UsuarioNombre { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public EstadoPresupuestoEnum Estado { get; init; }

    public bool EstaVigente { get; init; }

    public int CantidadRenglones { get; init; }

    /// <summary>Presupuesto que este corrige (PE-03), o <c>null</c> si no nació de una corrección.</summary>
    public int? IdPresupuestoReemplazado { get; init; }

    /// <summary>Presupuesto que reemplazó a este (PE-03), o <c>null</c> si no fue reemplazado.</summary>
    public int? IdPresupuestoReemplazante { get; init; }
}
