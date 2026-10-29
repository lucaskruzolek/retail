using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Proveedores;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación de persistencia y consultas push-down a SQL Server para catálogos de proveedores (Ley 2 y Ley 8).
/// </summary>
public class CatalogoProveedorQueryService : ICatalogoProveedorQueryService
{
    private readonly RetailDbContext _context;

    public CatalogoProveedorQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CatalogoPaginadoDto> ObtenerCatalogoPaginadoAsync(
        ConsultaCatalogoProveedorDto consulta,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CatalogoProveedor> query = _context.CatalogosProveedores.AsNoTracking();

        if (consulta.IdProveedor.HasValue && consulta.IdProveedor.Value > 0)
        {
            query = query.Where(c => c.IdProveedor == consulta.IdProveedor.Value);
        }

        if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
        {
            var term = consulta.TerminoBusqueda.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.CodigoProveedor, $"%{term}%") ||
                EF.Functions.Like(c.DescripcionProveedor, $"%{term}%") ||
                (c.CodigoBarras != null && EF.Functions.Like(c.CodigoBarras, $"%{term}%")));
        }

        // La vinculación se resuelve en SQL con subconsultas correlacionadas (Ley 8): el costo depende de la página,
        // no del tamaño de la tienda. Vinculado = fuente, principal o adicional, de un artículo activo (Track 4.3, J6):
        // el filtro global de borrado lógico descarta el artículo dado de baja aunque el ítem todavía lo apunte.
        if (consulta.EstadoVinculacion == EstadoVinculacionCatalogoEnum.SinIncorporar)
        {
            query = query.Where(c => !_context.Articulos.Any(a => a.Id == c.IdArticulo));
        }
        else if (consulta.EstadoVinculacion == EstadoVinculacionCatalogoEnum.YaEnTienda)
        {
            query = query.Where(c => _context.Articulos.Any(a => a.Id == c.IdArticulo));
        }

        int totalRegistros = await query.CountAsync(cancellationToken);

        int tamanoPagina = Math.Max(1, consulta.TamañoPagina);
        int pagina = Math.Max(1, consulta.Pagina);

        // OFFSET/FETCH se aplica sobre CATALOGOS_PROVEEDORES antes de proyectar, así las subconsultas correlacionadas
        // se evalúan solo para las filas de la página.
        // id_articulo apunta a un solo artículo (A-1), por lo que FirstOrDefault nunca elige entre varios candidatos.
        // Coincidencia por código de barras (RF-05): igualdad en SQL en lugar de Trim + OrdinalIgnoreCase en memoria.
        // Es equivalente porque la importación guarda los códigos ya recortados (ProveedorService.FiltrarFilasValidas)
        // y las columnas codigo_barras usan la intercalación por defecto de la base, insensible a mayúsculas. Con fuentes
        // múltiples coincide también un artículo que ya tiene fuentes: el ítem se le suma como adicional (Track 4.3, K4).
        // Una presentación derivada no cuenta, porque no puede tener fuentes (RF-21).
        var dtos = await query
            .OrderBy(c => c.DescripcionProveedor)
            .ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(c => new CatalogoProveedorDto
            {
                Id = c.Id,
                IdProveedor = c.IdProveedor,
                ProveedorRazonSocial = c.Proveedor != null ? c.Proveedor.RazonSocial : string.Empty,
                CodigoProveedor = c.CodigoProveedor,
                CodigoBarras = c.CodigoBarras,
                DescripcionProveedor = c.DescripcionProveedor,
                CostoReposicion = c.CostoReposicion,
                FechaActualizacion = c.FechaActualizacion,
                EstaVinculado = _context.Articulos.Any(a => a.Id == c.IdArticulo),
                CoincideConArticuloTienda =
                    !_context.Articulos.Any(a => a.Id == c.IdArticulo) &&
                    c.CodigoBarras != null &&
                    c.CodigoBarras != "" &&
                    _context.Articulos.Any(a => a.IdArticuloOrigen == null && a.CodigoBarras == c.CodigoBarras),
                IdArticuloVinculado = _context.Articulos
                    .Where(a => a.Id == c.IdArticulo)
                    .Select(a => (int?)a.Id)
                    .FirstOrDefault(),
                NombreArticuloTienda = _context.Articulos
                    .Where(a => a.Id == c.IdArticulo)
                    .Select(a => a.Descripcion)
                    .FirstOrDefault(),
                PrecioVentaTienda = _context.Articulos
                    .Where(a => a.Id == c.IdArticulo)
                    .Select(a => (decimal?)a.PrecioVenta)
                    .FirstOrDefault(),
                StockActualTienda = _context.Articulos
                    .Where(a => a.Id == c.IdArticulo)
                    .Select(a => (int?)a.StockActual)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // Se calcula sobre todo el catálogo vigente del proveedor, no sobre la consulta filtrada: la búsqueda no debe
        // cambiar la fecha de la lista. MaxAsync se traduce a MAX en SQL Server (Ley 8).
        DateTime? fechaUltimaPlanilla = null;
        if (consulta.IdProveedor is > 0)
        {
            fechaUltimaPlanilla = await _context.CatalogosProveedores
                .AsNoTracking()
                .Where(c => c.IdProveedor == consulta.IdProveedor.Value)
                .MaxAsync(c => (DateTime?)c.FechaActualizacion, cancellationToken);
        }

        return new CatalogoPaginadoDto
        {
            Items = dtos,
            TotalRegistros = totalRegistros,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina,
            FechaUltimaPlanilla = fechaUltimaPlanilla
        };
    }

    public async Task<CatalogoProveedor?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.CatalogosProveedores
            .Include(c => c.Proveedor)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogoProveedor>> ObtenerPorIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.CatalogosProveedores.Where(c => idList.Contains(c.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogoProveedor>> ObtenerFuentesDeArticuloAsync(int idArticulo, CancellationToken cancellationToken = default)
    {
        return await _context.CatalogosProveedores.Where(c => c.IdArticulo == idArticulo).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, CatalogoProveedor>> ObtenerMapaPorCodigosProveedorAsync(
        int idProveedor,
        IEnumerable<string> codigos,
        CancellationToken cancellationToken = default)
    {
        var codigoList = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var lista = await _context.CatalogosProveedores
            .Where(c => c.IdProveedor == idProveedor && codigoList.Contains(c.CodigoProveedor))
            .ToListAsync(cancellationToken);

        return lista.ToDictionary(c => c.CodigoProveedor, c => c);
    }

    public async Task AgregarAsync(CatalogoProveedor catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        await _context.CatalogosProveedores.AddAsync(catalogo, cancellationToken);
    }

    public Task ActualizarAsync(CatalogoProveedor catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        _context.CatalogosProveedores.Update(catalogo);
        return Task.CompletedTask;
    }
}
