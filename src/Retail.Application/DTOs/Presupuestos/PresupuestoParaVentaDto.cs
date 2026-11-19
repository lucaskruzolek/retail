using Retail.Application.DTOs.Ventas;

namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Presupuesto pendiente recuperado para cobrarlo en el POS (RF-12): renglones con el precio a cobrar, diferencias de
/// precio (solo si venció), faltantes de stock y artículos dados de baja.
/// </summary>
public record class PresupuestoParaVentaDto
{
    public required int IdPresupuesto { get; init; }

    /// <summary>Nota interna para la etiqueta del POS (PS-20).</summary>
    public string? Titulo { get; init; }

    public int? IdCliente { get; init; }

    public string? ClienteNombre { get; init; }

    public required bool EstaVigente { get; init; }

    public required DateOnly FechaVencimiento { get; init; }

    /// <summary>Descuento del presupuesto: valor inicial editable del ticket (PS-15).</summary>
    public required decimal Descuento { get; init; }

    /// <summary>Solo los renglones cuyo artículo sigue activo.</summary>
    public required IReadOnlyList<RenglonPresupuestoParaVentaDto> Renglones { get; init; }

    /// <summary>Solo se informan si el presupuesto está vencido y el precio cambió (PS-03).</summary>
    public required IReadOnlyList<DiscrepanciaPrecioPresupuestoDto> Discrepancias { get; init; }

    public required IReadOnlyList<FaltanteStockDto> Faltantes { get; init; }

    /// <summary>Descripciones de los artículos dados de baja, que quedan fuera de <see cref="Renglones"/>.</summary>
    public required IReadOnlyList<string> ArticulosNoDisponibles { get; init; }

    public bool RequiereConfirmacion =>
        (!EstaVigente && Discrepancias.Count > 0) || Faltantes.Count > 0 || ArticulosNoDisponibles.Count > 0;
}
