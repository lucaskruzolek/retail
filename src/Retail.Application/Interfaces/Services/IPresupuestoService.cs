using Retail.Application.DTOs.Presupuestos;

namespace Retail.Application.Interfaces.Services;

/// <summary>
/// Casos de uso del presupuesto comercial (RF-11, RF-12). La conversión en venta no está acá: ocurre dentro de
/// <see cref="IVentaService.RegistrarVentaAsync"/>, en la misma transacción que el cobro (PS-12).
/// </summary>
public interface IPresupuestoService
{
    /// <summary>
    /// Emite un presupuesto con los precios y descripciones actuales del catálogo. Rechaza el ticket si el precio
    /// visto por el cajero está desactualizado o si un artículo fue dado de baja. No requiere turno de caja (PS-17).
    /// </summary>
    Task<PresupuestoDto> EmitirAsync(CrearPresupuestoDto dto, CancellationToken cancellationToken = default);

    /// <summary>Lista los presupuestos con filtros y paginación en servidor.</summary>
    Task<PresupuestosPaginadosDto> ListarAsync(ConsultaPresupuestosDto consulta, CancellationToken cancellationToken = default);

    /// <summary>Detalle de un presupuesto, o <c>null</c> si no existe.</summary>
    Task<PresupuestoDto?> ObtenerAsync(int idPresupuesto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Arma lo que el POS necesita para cobrar un presupuesto pendiente: precio a cobrar por renglón, diferencias si
    /// venció, faltantes de stock y artículos dados de baja. No modifica nada.
    /// </summary>
    /// <exception cref="Retail.Domain.Exceptions.TransicionPresupuestoInvalidaException">Si no está pendiente.</exception>
    Task<PresupuestoParaVentaDto> RecuperarParaVentaAsync(int idPresupuesto, CancellationToken cancellationToken = default);

    /// <summary>Anula un presupuesto pendiente. No pide motivo (PS-16).</summary>
    Task AnularAsync(int idPresupuesto, CancellationToken cancellationToken = default);

    /// <summary>Imprime el presupuesto en el ticket térmico.</summary>
    Task ImprimirAsync(int idPresupuesto, CancellationToken cancellationToken = default);

    /// <summary>Exporta el presupuesto a PDF en <paramref name="destino"/> sin cerrarlo.</summary>
    Task ExportarPdfAsync(int idPresupuesto, Stream destino, CancellationToken cancellationToken = default);
}
