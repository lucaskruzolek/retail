using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Ventas;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
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
        IQueryable<Articulo> query = _context.Articulos.AsNoTracking();

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

        if (consulta.IdRubro.HasValue && consulta.IdRubro.Value > 0)
        {
            // El rubro se deriva de la categoría (RF-04): se resuelve con un JOIN a CATEGORIAS en SQL Server.
            int idRubro = consulta.IdRubro.Value;
            query = query.Where(a => a.Categoria != null && a.Categoria.IdRubro == idRubro);
        }

        if (consulta.IdMarca.HasValue && consulta.IdMarca.Value > 0)
        {
            query = query.Where(a => a.IdMarca == consulta.IdMarca.Value);
        }

        if (consulta.IdProveedor == -1)
        {
            // Ninguno (o manual): ni el artículo ni su artículo origen tienen fuentes, ni principal ni adicionales
            // (Track 4.3, K5). La presentación derivada se compra a través de su origen (RF-21).
            query = query.Where(a =>
                !_context.CatalogosProveedores.Any(c => c.IdArticulo == (a.IdArticuloOrigen ?? a.Id)));
        }
        else if (consulta.IdProveedor.HasValue && consulta.IdProveedor.Value > 0)
        {
            int idProv = consulta.IdProveedor.Value;
            // El distribuidor puede ser cualquier fuente del artículo, o de su origen si es una presentación derivada, no
            // solo la principal (Track 4.3, K5).
            query = query.Where(a =>
                _context.CatalogosProveedores.Any(c => c.IdProveedor == idProv && c.IdArticulo == (a.IdArticuloOrigen ?? a.Id)));
        }

        if (consulta.FiltroStock == FiltroStockArticuloEnum.StockCritico)
        {
            query = query.Where(a => !a.EsServicio && a.StockActual <= a.StockMinimo);
        }
        else if (consulta.FiltroStock == FiltroStockArticuloEnum.SinStock)
        {
            query = query.Where(a => !a.EsServicio && a.StockActual <= 0);
        }

        bool esConsultaFiltrada = !string.IsNullOrWhiteSpace(consulta.TerminoBusqueda) ||
            (consulta.IdCategoria.HasValue && consulta.IdCategoria.Value > 0) ||
            (consulta.IdRubro.HasValue && consulta.IdRubro.Value > 0) ||
            (consulta.IdMarca.HasValue && consulta.IdMarca.Value > 0) ||
            (consulta.IdProveedor.HasValue && consulta.IdProveedor.Value != 0) ||
            consulta.FiltroStock != FiltroStockArticuloEnum.Todos;

        // 3. Conteo de registros coincidentes (evita consulta COUNT duplicada si no hay filtros)
        int totalRegistros = esConsultaFiltrada
            ? await query.CountAsync(cancellationToken)
            : totalArticulos;

        int tamanoPagina = Math.Max(1, consulta.TamanoPagina);
        int pagina = Math.Max(1, consulta.Pagina);

        // 4. Paginación nativa en SQL Server (OFFSET ... ROWS FETCH NEXT ... ROWS ONLY)
        // El orden y la paginación se aplican sobre ARTICULOS antes de proyectar: los JOIN con categoría, marca y
        // proveedor se resuelven solo para las filas de la página. Id desempata de forma determinista.
        var items = await query
            .OrderBy(a => a.Descripcion)
            .ThenBy(a => a.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(a => new ArticuloDto
            {
                IdArticulo = a.Id,
                CodigoBarras = a.CodigoBarras,
                Descripcion = a.Descripcion,
                IdCategoria = a.IdCategoria,
                CategoriaNombre = a.Categoria != null ? a.Categoria.NombreCategoria : "Sin categoría",
                IdMarca = a.IdMarca,
                MarcaNombre = a.Marca != null ? a.Marca.NombreMarca : "Sin marca",
                IdCatalogoProveedor = a.IdCatalogoProveedor,
                IdArticuloOrigen = a.IdArticuloOrigen,
                UnidadesPorOrigen = a.UnidadesPorOrigen,
                ProveedorNombre = a.CatalogoProveedor != null && a.CatalogoProveedor.Proveedor != null
                    ? a.CatalogoProveedor.Proveedor.RazonSocial
                    : null,
                CodigoProveedor = a.CatalogoProveedor != null ? a.CatalogoProveedor.CodigoProveedor : null,
                DescripcionProveedor = a.CatalogoProveedor != null ? a.CatalogoProveedor.DescripcionProveedor : null,
                CostoCatalogoProveedor = a.CatalogoProveedor != null ? a.CatalogoProveedor.CostoReposicion : null,
                CostoReposicion = a.CostoReposicion,
                PorcentajeGanancia = a.PorcentajeGanancia,
                PrecioVenta = a.PrecioVenta,
                StockActual = a.StockActual,
                StockMinimo = a.StockMinimo,
                EsServicio = a.EsServicio
            })
            .ToListAsync(cancellationToken);

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
