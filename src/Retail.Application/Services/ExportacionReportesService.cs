using Retail.Application.DTOs.Reportes;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services.Reportes;
using Retail.Domain.Entities;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Exporta a Excel o PDF el reporte que el Gerente tiene en pantalla (RF-26). Traduce el resultado recibido a un
/// documento neutro (D-12) y delega la escritura en <see cref="IExportadorReportes"/>; nunca vuelve a consultar.
/// </summary>
public class ExportacionReportesService : IExportacionReportesService
{
    private readonly IExportadorReportes _exportador;
    private readonly IRepository<Usuario> _usuarios;

    public ExportacionReportesService(IExportadorReportes exportador, IRepository<Usuario> usuarios)
    {
        _exportador = exportador ?? throw new ArgumentNullException(nameof(exportador));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
    }

    public async Task ExportarAsync(
        int idActor,
        ResultadoReporteDto resultado,
        FormatoExportacionEnum formato,
        Stream destino,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(destino);

        var actor = idActor > 0
            ? await _usuarios.GetByIdAsync(idActor, includeDeleted: false, cancellationToken)
            : null;

        if (actor is null)
        {
            throw new OperacionNoAutorizadaException("La operación fue solicitada por un usuario inexistente o dado de baja.");
        }

        if (!actor.EsGerente())
        {
            throw new OperacionNoAutorizadaException("Solo un Gerente puede exportar los reportes gerenciales.");
        }

        var documento = ConstructorDocumentoReporte.Crear(resultado, actor.NombreCompleto);
        await _exportador.ExportarAsync(documento, formato, destino, cancellationToken);
    }
}
