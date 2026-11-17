using Retail.Application.DTOs.Presupuestos;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Genera el PDF A4 de un presupuesto (PS-07). Escribe en el <see cref="Stream"/> recibido y no lo cierra, igual que
/// <see cref="IExportadorReportes"/>: la ruta la elige la interfaz, de modo que un fallo de escritura se reintenta.
/// </summary>
public interface IExportadorPresupuestoPdf
{
    /// <summary>
    /// Escribe el presupuesto en <paramref name="destino"/>. <b>No debe incluir el título</b> (PS-20): es una nota de uso
    /// interno que nunca se entrega al cliente.
    /// </summary>
    Task ExportarAsync(PresupuestoDto presupuesto, Stream destino, CancellationToken cancellationToken = default);
}
