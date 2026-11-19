using Retail.Application.DTOs.Ventas;
using Retail.Domain.Services;

namespace Retail.Application.Services;

/// <summary>
/// Cálculo compartido del faltante de stock de un artículo (RF-10, RF-21): lo usan la verificación del ticket de
/// <see cref="VentaService"/> y la recuperación de presupuestos de <see cref="PresupuestoService"/>.
/// </summary>
internal static class FaltantesStock
{
    public static FaltanteStockDto Crear(DisponibilidadArticuloDto disponible, int cantidadSolicitada)
    {
        // Solo una presentación con su origen activo se puede reponer fraccionando (RF-21).
        int origenesAFraccionar = disponible.UnidadesPorOrigen is int unidadesPorOrigen && disponible.StockOrigen.HasValue
            ? ServicioFraccionamiento.CalcularOrigenesNecesarios(cantidadSolicitada - disponible.StockActual, unidadesPorOrigen)
            : 0;

        return new FaltanteStockDto
        {
            IdArticulo = disponible.IdArticulo,
            Descripcion = disponible.Descripcion,
            CantidadSolicitada = cantidadSolicitada,
            StockActual = disponible.StockActual,
            DescripcionOrigen = disponible.DescripcionOrigen,
            UnidadesPorOrigen = disponible.UnidadesPorOrigen,
            StockOrigen = disponible.StockOrigen,
            OrigenesAFraccionar = origenesAFraccionar
        };
    }
}
