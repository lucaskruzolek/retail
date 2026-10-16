using Retail.Application.DTOs.Reportes;

namespace Retail.Application.Interfaces.Services;

/// <summary>Exportación de reportes a Excel o PDF (RF-26). Exclusiva del Gerente (D-11).</summary>
public interface IExportacionReportesService
{
    /// <summary>
    /// Exporta EXACTAMENTE el resultado recibido (lo que se ve en pantalla), sin volver a consultar la base. No cierra
    /// el <paramref name="destino"/>.
    /// </summary>
    Task ExportarAsync(
        int idActor,
        ResultadoReporteDto resultado,
        FormatoExportacionEnum formato,
        Stream destino,
        CancellationToken cancellationToken = default);
}
