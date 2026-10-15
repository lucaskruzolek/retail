using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Exportacion;
using Retail.Application.Interfaces.Infrastructure;

namespace Retail.Infrastructure.Reportes;

/// <summary>
/// Implementa <see cref="IExportadorReportes"/> (RF-26): valida los argumentos y delega en el exportador del formato.
/// No tiene estado, por lo que se registra como Singleton. No cierra el <see cref="Stream"/> recibido.
/// </summary>
public sealed class ExportadorReportes : IExportadorReportes
{
    public async Task ExportarAsync(
        DocumentoReporte documento,
        FormatoExportacionEnum formato,
        Stream destino,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(destino);

        if (!Enum.IsDefined(formato))
        {
            throw new ArgumentOutOfRangeException(nameof(formato), formato, "El formato de exportación no es válido.");
        }

        if (!destino.CanWrite)
        {
            throw new ArgumentException("El destino de la exportación debe permitir escritura.", nameof(destino));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // MiniExcel y QuestPDF son síncronos y consumen CPU: se sacan del hilo de la interfaz (Ley 4).
        await Task.Run(
            () =>
            {
                if (formato == FormatoExportacionEnum.Excel)
                {
                    ExportadorExcel.Exportar(documento, destino);
                }
                else
                {
                    ExportadorPdf.Exportar(documento, destino);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }
}
