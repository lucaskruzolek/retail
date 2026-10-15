using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Exportacion;

namespace Retail.Application.Interfaces.Infrastructure;

/// <summary>
/// Convierte un <see cref="DocumentoReporte"/> en un archivo (RF-26). Escribe en el <see cref="Stream"/> recibido y
/// no lo cierra; la ruta la elige la interfaz, de modo que un fallo de escritura se reintenta con el mismo documento.
/// </summary>
public interface IExportadorReportes
{
    /// <summary>Escribe el documento en el formato pedido.</summary>
    Task ExportarAsync(
        DocumentoReporte documento,
        FormatoExportacionEnum formato,
        Stream destino,
        CancellationToken cancellationToken = default);
}
