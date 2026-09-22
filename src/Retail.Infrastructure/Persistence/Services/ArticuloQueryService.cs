using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
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
}
