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
    private readonly IRepository<Categoria> _categorias;
    private readonly IRepository<Marca> _marcas;

    public ExportacionReportesService(
        IExportadorReportes exportador,
        IRepository<Usuario> usuarios,
        IRepository<Categoria> categorias,
        IRepository<Marca> marcas)
    {
        _exportador = exportador ?? throw new ArgumentNullException(nameof(exportador));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _categorias = categorias ?? throw new ArgumentNullException(nameof(categorias));
        _marcas = marcas ?? throw new ArgumentNullException(nameof(marcas));
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

        var nombres = await ResolverNombresAsync(resultado, cancellationToken);
        var documento = ConstructorDocumentoReporte.Crear(resultado, actor.NombreCompleto, nombres);
        await _exportador.ExportarAsync(documento, formato, destino, cancellationToken);
    }

    /// <summary>
    /// Resuelve el nombre de la categoría, la marca y el cajero que el filtro del resultado referencia por id (D-24). Se
    /// buscan incluyendo los dados de baja: un filtro histórico sigue mostrando el nombre correcto. Un id inexistente
    /// se informa como "#id". Las búsquedas son en secuencia porque los repositorios comparten el mismo DbContext, que
    /// no admite dos operaciones simultáneas (mismo criterio que <c>ReporteService</c>).
    /// </summary>
    private async Task<NombresFiltro> ResolverNombresAsync(ResultadoReporteDto resultado, CancellationToken cancellationToken)
    {
        var (idCategoria, idMarca, idUsuario) = resultado switch
        {
            VentasPorPeriodoDto ventas => ((int?)null, (int?)null, ventas.Filtro.IdUsuario),
            RankingArticulosDto ranking => (ranking.Filtro.IdCategoria, ranking.Filtro.IdMarca, (int?)null),
            RentabilidadDto rentabilidad => (rentabilidad.Filtro.IdCategoria, rentabilidad.Filtro.IdMarca, (int?)null),
            StockCriticoDto stock => (stock.Filtro.IdCategoria, stock.Filtro.IdMarca, (int?)null),
            SinMovimientoDto sinMovimiento => (sinMovimiento.Filtro.IdCategoria, sinMovimiento.Filtro.IdMarca, (int?)null),
            _ => ((int?)null, (int?)null, (int?)null)
        };

        string? categoria = null;
        if (idCategoria is { } idC)
        {
            var entidad = await _categorias.GetByIdAsync(idC, includeDeleted: true, cancellationToken);
            categoria = entidad?.NombreCategoria ?? $"#{idC}";
        }

        string? marca = null;
        if (idMarca is { } idM)
        {
            var entidad = await _marcas.GetByIdAsync(idM, includeDeleted: true, cancellationToken);
            marca = entidad?.NombreMarca ?? $"#{idM}";
        }

        string? cajero = null;
        if (idUsuario is { } idU)
        {
            var entidad = await _usuarios.GetByIdAsync(idU, includeDeleted: true, cancellationToken);
            cajero = entidad?.NombreCompleto ?? $"#{idU}";
        }

        return new NombresFiltro(categoria, marca, cajero);
    }
}
