using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Listas;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de solo lectura sobre listas de artículos (RF-30): búsqueda, orden, paginación y totales se evalúan en
/// SQL Server y se proyectan directamente a DTOs, sin Change Tracker (Ley 8).
/// </summary>
public class ListaQueryService : IListaQueryService
{
    private const string CaracterDeEscape = "\\";

    private readonly RetailDbContext _context;

    public ListaQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ListasPaginadosDto> ListarAsync(ConsultaListasDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // Los filtros globales de borrado lógico quedan activos: solo cuentan las listas, los renglones y los artículos
        // vigentes, que es lo que muestra la grilla.
        IQueryable<ListaArticulos> query = _context.Listas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
        {
            var patron = $"%{EscaparParaLike(consulta.TerminoBusqueda.Trim())}%";

            // La columna nombre tiene collation insensible a acentos y mayúsculas: "musica" encuentra "Música".
            query = query.Where(l => EF.Functions.Like(l.Nombre, patron, CaracterDeEscape));
        }

        var totalRegistros = await query.CountAsync(cancellationToken);

        var tamanoPagina = Math.Max(1, consulta.TamanoPagina);
        var pagina = Math.Max(1, consulta.Pagina);

        // R-2: se pagina antes de proyectar, así los agregados (conteo, suma, ventas) se calculan solo para la página.
        var items = await query
            .OrderBy(l => l.Nombre)
            .ThenBy(l => l.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(l => new ListaResumenDto
            {
                IdLista = l.Id,
                Nombre = l.Nombre,
                CantidadArticulos = l.Renglones
                    .Count(r => r.DeletedAt == null && r.Articulo != null && r.Articulo.DeletedAt == null),
                TotalEstimado = l.Renglones
                    .Where(r => r.DeletedAt == null && r.Articulo != null && r.Articulo.DeletedAt == null)
                    .Sum(r => (decimal?)(r.Cantidad * r.Articulo!.PrecioVenta)) ?? 0m,
                CantidadVentas = _context.Ventas.Count(v => v.DeletedAt == null && v.IdListaOrigen == l.Id),
                FechaModificacion = l.FechaModificacion
            })
            .ToListAsync(cancellationToken);

        return new ListasPaginadosDto
        {
            Items = items,
            TotalRegistros = totalRegistros,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina
        };
    }

    public async Task<ListaDto?> ObtenerAsync(int idLista, CancellationToken cancellationToken = default)
    {
        // Se ignoran los filtros globales para que un artículo dado de baja siga apareciendo en la lista (marcado como
        // no disponible, L-13); por eso se exige a mano que la lista y los renglones no estén borrados.
        var cabecera = await _context.Listas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(l => l.Id == idLista && l.DeletedAt == null)
            .Select(l => new
            {
                l.Id,
                l.Nombre,
                l.FechaModificacion,
                Renglones = l.Renglones
                    .Where(r => r.DeletedAt == null)
                    .OrderBy(r => r.Id)
                    .Select(r => new RenglonListaDto
                    {
                        IdArticulo = r.IdArticulo,
                        CodigoBarras = r.Articulo!.CodigoBarras,
                        Descripcion = r.Articulo.Descripcion,
                        Cantidad = r.Cantidad,
                        PrecioActual = r.Articulo.PrecioVenta,
                        Subtotal = r.Cantidad * r.Articulo.PrecioVenta,
                        EstaDisponible = r.Articulo.DeletedAt == null
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        return new ListaDto
        {
            IdLista = cabecera.Id,
            Nombre = cabecera.Nombre,
            FechaModificacion = cabecera.FechaModificacion,
            Renglones = cabecera.Renglones,
            // Solo los renglones disponibles suman al total (L-13).
            TotalEstimado = cabecera.Renglones.Where(r => r.EstaDisponible).Sum(r => r.Subtotal)
        };
    }

    /// <summary>Escapa los comodines de LIKE (%, _, [) y el propio carácter de escape.</summary>
    private static string EscaparParaLike(string texto)
    {
        return texto
            .Replace(CaracterDeEscape, CaracterDeEscape + CaracterDeEscape, StringComparison.Ordinal)
            .Replace("%", CaracterDeEscape + "%", StringComparison.Ordinal)
            .Replace("_", CaracterDeEscape + "_", StringComparison.Ordinal)
            .Replace("[", CaracterDeEscape + "[", StringComparison.Ordinal);
    }
}
