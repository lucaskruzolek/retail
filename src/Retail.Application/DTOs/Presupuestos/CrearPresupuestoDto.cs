using Retail.Domain.Entities;

namespace Retail.Application.DTOs.Presupuestos;

/// <summary>
/// Parámetros para emitir un presupuesto desde el ticket del POS (RF-11). No afecta stock ni caja (PS-17).
/// </summary>
/// <remarks>
/// El <see cref="DetallePresupuestoDto.PrecioUnitarioPactado"/> de cada ítem es el precio <b>visto</b> en el ticket:
/// el servidor lo compara con el catálogo y nunca lo usa como precio (D-11).
/// </remarks>
public record class CrearPresupuestoDto
{
    public required int IdUsuario { get; init; }

    /// <summary>Cliente del presupuesto; <c>null</c> es Consumidor Final.</summary>
    public int? IdCliente { get; init; }

    /// <summary>Nota interna opcional (PS-20): nunca se entrega al cliente.</summary>
    public string? Titulo { get; init; }

    public decimal Descuento { get; init; } = 0;

    /// <summary>Días de validez, entre <see cref="Presupuesto.VigenciaMinimaDias"/> y <see cref="Presupuesto.VigenciaMaximaDias"/>.</summary>
    public int VigenciaDias { get; init; } = Presupuesto.VigenciaPorDefectoDias;

    public required IReadOnlyList<DetallePresupuestoDto> Items { get; init; }
}
