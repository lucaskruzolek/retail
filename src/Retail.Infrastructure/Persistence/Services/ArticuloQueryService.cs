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

        // Filtros de selección múltiple: cada lista se traduce a IN (SELECT value FROM OPENJSON(@lista)), un único
        // parámetro sin el tope de 2.100 de SQL Server. Lista vacía = sin filtro.
        if (consulta.IdsCategoria.Count > 0)
        {
            // El filtro por rubro llega como el conjunto de sus categorías (RF-04).
            List<int> idsCategoria = consulta.IdsCategoria.Distinct().ToList();
            query = query.Where(a => a.IdCategoria != null && idsCategoria.Contains(a.IdCategoria.Value));
        }

        if (consulta.IdsMarca.Count > 0)
        {
            List<int> idsMarca = consulta.IdsMarca.Distinct().ToList();
            query = query.Where(a => a.IdMarca != null && idsMarca.Contains(a.IdMarca.Value));
        }

        if (consulta.IdsProveedor.Count > 0)
        {
            query = FiltrarPorDistribuidores(query, consulta.IdsProveedor);
        }

        if (consulta.EstadosStock.Count > 0)
        {
            query = FiltrarPorEstadosDeStock(query, consulta.EstadosStock);
        }

        bool esConsultaFiltrada = !string.IsNullOrWhiteSpace(consulta.TerminoBusqueda) ||
            consulta.IdsCategoria.Count > 0 ||
            consulta.IdsMarca.Count > 0 ||
            consulta.IdsProveedor.Count > 0 ||
            consulta.EstadosStock.Count > 0;

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
                // Subconsulta correlacionada, solo para las filas de la página (A-19); el filtro global omite las bajas.
                CantidadFuentes = _context.CatalogosProveedores.Count(c => c.IdArticulo == a.Id),
                // Sin principal se pide a la más barata, con el mismo orden que el combo del modal (costo, id).
                IdFuentePedido = a.IdCatalogoProveedor ?? _context.CatalogosProveedores
                    .Where(c => c.IdArticulo == a.Id)
                    .OrderBy(c => c.CostoReposicion)
                    .ThenBy(c => c.Id)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefault(),
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

    public async Task<IReadOnlyList<FuenteArticuloDto>> ObtenerFuentesAsync(int idArticulo, CancellationToken cancellationToken = default)
    {
        // Proyección sin tracking (Ley 8): el formulario solo muestra las fuentes; al guardar, el servicio las vuelve a
        // leer con tracking para modificarlas (A-19).
        return await _context.CatalogosProveedores
            .AsNoTracking()
            .Where(c => c.IdArticulo == idArticulo)
            .OrderBy(c => c.CostoReposicion)
            .ThenBy(c => c.Id)
            .Select(c => new FuenteArticuloDto
            {
                IdCatalogoProveedor = c.Id,
                IdProveedor = c.IdProveedor,
                RazonSocialProveedor = c.Proveedor!.RazonSocial,
                CodigoProveedor = c.CodigoProveedor,
                Descripcion = c.DescripcionProveedor,
                CostoActual = c.CostoReposicion,
                FechaActualizacion = c.FechaActualizacion,
                EsPrincipal = c.Articulo != null && c.Articulo.IdCatalogoProveedor == c.Id
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

    /// <summary>
    /// Distribuidores elegidos combinados con O. Cuenta cualquier fuente del artículo (no solo la principal) o, en una
    /// presentación derivada, las de su artículo origen, que es por donde se compra (Track 4.3, K5; RF-21).
    /// <see cref="ConsultaArticulosDto.IdProveedorManual"/> admite además los artículos sin ninguna fuente.
    /// </summary>
    private IQueryable<Articulo> FiltrarPorDistribuidores(IQueryable<Articulo> query, IReadOnlyList<int> idsProveedor)
    {
        bool incluirManuales = idsProveedor.Contains(ConsultaArticulosDto.IdProveedorManual);
        List<int> idsReales = idsProveedor.Where(id => id > 0).Distinct().ToList();

        return query.Where(a =>
            (incluirManuales && !_context.CatalogosProveedores.Any(c => c.IdArticulo == (a.IdArticuloOrigen ?? a.Id))) ||
            _context.CatalogosProveedores.Any(c => idsReales.Contains(c.IdProveedor) && c.IdArticulo == (a.IdArticuloOrigen ?? a.Id)));
    }

    /// <summary>
    /// Estados de stock elegidos combinados con O. Los estados son disjuntos (ver <see cref="FiltroStockArticuloEnum"/>)
    /// y los servicios, que no llevan stock, quedan afuera. Cada estado elegido llega a SQL como un parámetro booleano:
    /// la condición es una sola y el plan de ejecución se reutiliza para cualquier combinación.
    /// </summary>
    private static IQueryable<Articulo> FiltrarPorEstadosDeStock(IQueryable<Articulo> query, IReadOnlyList<FiltroStockArticuloEnum> estados)
    {
        bool sinStock = estados.Contains(FiltroStockArticuloEnum.SinStock);
        bool stockBajo = estados.Contains(FiltroStockArticuloEnum.StockBajo);
        bool conStock = estados.Contains(FiltroStockArticuloEnum.ConStock);

        return query.Where(a => !a.EsServicio && (
            (sinStock && a.StockActual <= 0) ||
            (stockBajo && a.StockActual > 0 && a.StockActual <= a.StockMinimo) ||
            (conStock && a.StockActual > 0 && a.StockActual > a.StockMinimo)));
    }
}
