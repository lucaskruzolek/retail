using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Implementación de consultas optimizadas y paginación para el catálogo de artículos con push-down a SQL Server (Ley 8).
/// </summary>
public class ArticuloQueryService : IArticuloQueryService
{
    private readonly RetailDbContext _context;

    public ArticuloQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ArticulosPaginadosDto> ObtenerArticulosPaginadosAsync(
        ConsultaArticulosDto consulta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        // 1. Agregaciones globales en servidor (Ley 8)
        int totalArticulos = await _context.Articulos.CountAsync(cancellationToken);
        int totalAlertasStock = await _context.Articulos.CountAsync(a => !a.EsServicio && a.StockActual <= a.StockMinimo, cancellationToken);

        // 2. Consulta filtrada con proyección y sin tracking (RNF-03)
        IQueryable<Articulo> query = _context.Articulos
            .AsNoTracking()
            .Include(a => a.Categoria)
            .Include(a => a.Marca)
            .Include(a => a.CatalogoProveedor)
                .ThenInclude(cp => cp!.Proveedor);

        if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
        {
            var term = consulta.TerminoBusqueda.Trim();
            query = query.Where(a =>
                (a.CodigoBarras != null && EF.Functions.Like(a.CodigoBarras, $"%{term}%")) ||
                EF.Functions.Like(a.Descripcion, $"%{term}%") ||
                (a.Marca != null && EF.Functions.Like(a.Marca.NombreMarca, $"%{term}%")));
        }

        if (consulta.IdCategoria.HasValue && consulta.IdCategoria.Value > 0)
        {
            query = query.Where(a => a.IdCategoria == consulta.IdCategoria.Value);
        }

        if (consulta.SoloStockCritico)
        {
            query = query.Where(a => !a.EsServicio && a.StockActual <= a.StockMinimo);
        }

        bool esConsultaFiltrada = !string.IsNullOrWhiteSpace(consulta.TerminoBusqueda) ||
            (consulta.IdCategoria.HasValue && consulta.IdCategoria.Value > 0) ||
            consulta.SoloStockCritico;

        // 3. Conteo de registros coincidentes (evita consulta COUNT duplicada si no hay filtros)
        int totalRegistros = esConsultaFiltrada
            ? await query.CountAsync(cancellationToken)
            : totalArticulos;

        int tamanoPagina = Math.Max(1, consulta.TamanoPagina);
        int pagina = Math.Max(1, consulta.Pagina);

        // 4. Paginación nativa en SQL Server (OFFSET ... ROWS FETCH NEXT ... ROWS ONLY)
        var entidades = await query
            .OrderBy(a => a.Descripcion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        var items = entidades.Select(a => new ArticuloDto
        {
            IdArticulo = a.Id,
            CodigoBarras = a.CodigoBarras,
            Descripcion = a.Descripcion,
            IdCategoria = a.IdCategoria,
            CategoriaNombre = a.Categoria?.NombreCategoria ?? "Sin categoría",
            IdMarca = a.IdMarca,
            MarcaNombre = a.Marca?.NombreMarca ?? "Sin marca",
            IdCatalogoProveedor = a.IdCatalogoProveedor,
            IdArticuloOrigen = a.IdArticuloOrigen,
            UnidadesPorOrigen = a.UnidadesPorOrigen,
            ProveedorNombre = a.CatalogoProveedor?.Proveedor?.RazonSocial,
            CodigoProveedor = a.CatalogoProveedor?.CodigoProveedor,
            DescripcionProveedor = a.CatalogoProveedor?.DescripcionProveedor,
            CostoCatalogoProveedor = a.CatalogoProveedor?.CostoReposicion,
            CostoReposicion = a.CostoReposicion,
            PorcentajeGanancia = a.PorcentajeGanancia,
            PrecioVenta = a.PrecioVenta,
            StockActual = a.StockActual,
            StockMinimo = a.StockMinimo,
            EsServicio = a.EsServicio
        }).ToList();

        return new ArticulosPaginadosDto
        {
            Items = items,
            TotalRegistros = totalRegistros,
            TotalArticulos = totalArticulos,
            TotalAlertasStock = totalAlertasStock,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina
        };
    }

    public async Task<ArticuloVentaDto?> ObtenerParaVentaPorCodigoBarrasAsync(
        string codigoBarras,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoBarras);

        // El índice único filtrado de codigo_barras garantiza a lo sumo un artículo activo.
        return await ProyectarParaVenta(_context.Articulos.Where(a => a.CodigoBarras == codigoBarras))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArticuloVentaDto>> BuscarParaVentaAsync(
        string termino,
        int limite,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termino);

        var query = _context.Articulos
            .Where(a => a.Descripcion.Contains(termino) || (a.CodigoBarras != null && a.CodigoBarras.Contains(termino)))
            .OrderBy(a => a.Descripcion)
            .Take(Math.Max(1, limite));

        return await ProyectarParaVenta(query).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DisponibilidadArticuloDto>> ObtenerDisponibilidadAsync(
        IReadOnlyCollection<int> idsArticulos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idsArticulos);

        if (idsArticulos.Count == 0)
        {
            return Array.Empty<DisponibilidadArticuloDto>();
        }

        // Un solo viaje a la base: Contains se traduce con OPENJSON y el origen con un LEFT JOIN. El filtro global
        // de soft delete también se aplica al origen, así que un origen dado de baja llega como nulo.
        return await _context.Articulos
            .AsNoTracking()
            .Where(a => idsArticulos.Contains(a.Id))
            .Select(a => new DisponibilidadArticuloDto
            {
                IdArticulo = a.Id,
                Descripcion = a.Descripcion,
                PrecioVenta = a.PrecioVenta,
                StockActual = a.StockActual,
                EsServicio = a.EsServicio,
                IdArticuloOrigen = a.IdArticuloOrigen,
                DescripcionOrigen = a.ArticuloOrigen != null ? a.ArticuloOrigen.Descripcion : null,
                UnidadesPorOrigen = a.UnidadesPorOrigen,
                StockOrigen = a.ArticuloOrigen != null ? a.ArticuloOrigen.StockActual : null
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Proyección de solo lectura para el mostrador: SQL Server devuelve solo las columnas del DTO y el ChangeTracker
    /// no registra ninguna entidad (Ley 8, Ley 9 de persistencia).
    /// </summary>
    private static IQueryable<ArticuloVentaDto> ProyectarParaVenta(IQueryable<Articulo> query)
    {
        return query
            .AsNoTracking()
            .Select(a => new ArticuloVentaDto
            {
                IdArticulo = a.Id,
                CodigoBarras = a.CodigoBarras,
                Descripcion = a.Descripcion,
                PrecioVenta = a.PrecioVenta,
                StockActual = a.StockActual,
                EsServicio = a.EsServicio
            });
    }
}
