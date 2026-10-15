using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Reportes;
using Retail.Application.DTOs.Reportes.Consultas;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de lectura de los reportes gerenciales (RF-24, RF-25, RNF-09). Todas componen <c>IQueryable</c> con
/// <c>AsNoTracking</c> y se resuelven completas en SQL Server (Ley 8): GROUP BY, SUM, COUNT, TOP y subconsultas.
/// No usa vistas ni procedimientos almacenados (D-6): el prorrateo del descuento vive en <see cref="ItemsVendidos"/>.
/// </summary>
public class ReporteQueryService : IReporteQueryService
{
    // 2001-01-01 fue lunes: dividir por 7 los días transcurridos desde esta ancla numera las semanas que empiezan el
    // lunes (D-10), con todo el cálculo en SQL (DATEDIFF). Es válido para cualquier fecha posterior al ancla.
    private static readonly DateTime AnclaLunes = new(2001, 1, 1);

    private readonly RetailDbContext _context;

    public ReporteQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Un ítem vendido con su ingreso neto (descuento prorrateado, D-4) y su costo histórico.</summary>
    /// <remarks>
    /// Se proyecta con inicializador de objeto (no con constructor posicional): EF Core solo sabe reducir
    /// <c>new T { P = x }.P</c> a <c>x</c> cuando después se agrupa o filtra por una propiedad.
    /// </remarks>
    internal sealed record ItemVendido
    {
        public int IdVenta { get; init; }

        public int IdUsuario { get; init; }

        public DateTime FechaHora { get; init; }

        public int IdArticulo { get; init; }

        public int? IdCategoria { get; init; }

        public int? IdMarca { get; init; }

        public int Cantidad { get; init; }

        public decimal IngresoNeto { get; init; }

        public decimal Costo { get; init; }
    }

    public async Task<ResumenVentasFila> ObtenerResumenVentasAsync(RangoUtcDto rango, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rango);

        var totales = await VentasDelRango(rango)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Cantidad = g.Count(),
                Total = g.Sum(v => v.Total),
                Descuentos = g.Sum(v => v.Descuento)
            })
            .FirstOrDefaultAsync(cancellationToken);

        decimal costo = await ItemsVendidos(rango).SumAsync(i => (decimal?)i.Costo, cancellationToken) ?? 0m;

        return new ResumenVentasFila(totales?.Cantidad ?? 0, totales?.Total ?? 0m, totales?.Descuentos ?? 0m, costo);
    }

    public async Task<IReadOnlyList<VentasPeriodoFila>> ObtenerVentasPorPeriodoAsync(
        RangoUtcDto rango,
        AgrupacionTemporalEnum agrupacion,
        int? idUsuario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rango);

        IQueryable<Venta> ventas = VentasDelRango(rango);
        if (idUsuario.HasValue)
        {
            ventas = ventas.Where(v => v.IdUsuario == idUsuario.Value);
        }

        return agrupacion switch
        {
            AgrupacionTemporalEnum.Dia => await AgruparPorDiaAsync(ventas, rango.DesfaseMinutos, cancellationToken),
            AgrupacionTemporalEnum.Semana => await AgruparPorSemanaAsync(ventas, rango.DesfaseMinutos, cancellationToken),
            AgrupacionTemporalEnum.Mes => await AgruparPorMesAsync(ventas, rango.DesfaseMinutos, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(agrupacion), agrupacion, "La agrupación temporal no es válida.")
        };
    }

    public async Task<IReadOnlyList<RankingFila>> ObtenerMasVendidosAsync(
        RangoUtcDto rango,
        FiltroRankingDto filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rango);
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<ItemVendido> items = FiltrarItems(ItemsVendidos(rango), filtro.IdCategoria, filtro.IdMarca);

        var agrupados = items
            .GroupBy(i => i.IdArticulo)
            .Select(g => new
            {
                IdArticulo = g.Key,
                Unidades = g.Sum(i => i.Cantidad),
                Ingreso = g.Sum(i => i.IngresoNeto)
            });

        var ordenados = filtro.Criterio == CriterioRankingEnum.Importe
            ? agrupados.OrderByDescending(x => x.Ingreso).ThenBy(x => x.IdArticulo)
            : agrupados.OrderByDescending(x => x.Unidades).ThenBy(x => x.IdArticulo);

        var top = await ordenados.Take(filtro.Cantidad).ToListAsync(cancellationToken);
        if (top.Count == 0)
        {
            return [];
        }

        // Segunda consulta acotada al TOP (como máximo 100 ids): trae los datos descriptivos, incluso de artículos dados de baja.
        var ids = top.Select(x => x.IdArticulo).ToList();
        var datos = await DatosDeArticulosAsync(ids, cancellationToken);

        return top
            .Select(x =>
            {
                var a = datos[x.IdArticulo];
                return new RankingFila(x.IdArticulo, a.Descripcion, a.CodigoBarras, a.Categoria, a.Marca, x.Unidades, x.Ingreso);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<RankingFila>> ObtenerMenosVendidosAsync(
        RangoUtcDto rango,
        FiltroRankingDto filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rango);
        ArgumentNullException.ThrowIfNull(filtro);

        // Los artículos dados de baja no se pueden reponer: no aparecen (a diferencia de ItemsVendidos). El filtro es
        // manual porque IgnoreQueryFilters de la subconsulta de ventas desactiva los filtros globales de TODA la consulta.
        IQueryable<Articulo> articulos = _context.Articulos.AsNoTracking().Where(a => a.DeletedAt == null);
        if (filtro.IdCategoria.HasValue)
        {
            articulos = articulos.Where(a => a.IdCategoria == filtro.IdCategoria.Value);
        }

        if (filtro.IdMarca.HasValue)
        {
            articulos = articulos.Where(a => a.IdMarca == filtro.IdMarca.Value);
        }

        // LEFT JOIN de los artículos con los ítems vendidos y una sola agrupación: los artículos sin ventas quedan
        // con SUM nulo (=> 0). Más rápido que dos subconsultas correlacionadas por artículo (RNF-09), y EF no
        // traduce un LEFT JOIN contra una subconsulta ya agrupada.
        var filas = (from a in articulos
                     join i in ItemsVendidos(rango) on a.Id equals i.IdArticulo into itemsDelArticulo
                     from i in itemsDelArticulo.DefaultIfEmpty()
                     group i by new
                     {
                         a.Id,
                         a.Descripcion,
                         a.CodigoBarras,
                         Categoria = a.Categoria == null ? null : a.Categoria.NombreCategoria,
                         Marca = a.Marca == null ? null : a.Marca.NombreMarca
                     } into g
                     select new
                     {
                         g.Key.Id,
                         g.Key.Descripcion,
                         g.Key.CodigoBarras,
                         g.Key.Categoria,
                         g.Key.Marca,
                         Unidades = g.Sum(x => (int?)x.Cantidad) ?? 0,
                         Ingreso = g.Sum(x => (decimal?)x.IngresoNeto) ?? 0m
                     });

        var ordenadas = filtro.Criterio == CriterioRankingEnum.Importe
            ? filas.OrderBy(x => x.Ingreso).ThenBy(x => x.Descripcion)
            : filas.OrderBy(x => x.Unidades).ThenBy(x => x.Descripcion);

        var top = await ordenadas.Take(filtro.Cantidad).ToListAsync(cancellationToken);

        return top
            .Select(x => new RankingFila(x.Id, x.Descripcion, x.CodigoBarras, x.Categoria, x.Marca, x.Unidades, x.Ingreso))
            .ToList();
    }

    public async Task<IReadOnlyList<RentabilidadFila>> ObtenerRentabilidadAsync(
        RangoUtcDto rango,
        FiltroRentabilidadDto filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rango);
        ArgumentNullException.ThrowIfNull(filtro);

        IQueryable<ItemVendido> items = RentabilidadItems(rango, filtro);

        return filtro.AgruparPor switch
        {
            AgrupacionRentabilidadEnum.Categoria => await RentabilidadPorCategoriaAsync(items, cancellationToken),
            AgrupacionRentabilidadEnum.Marca => await RentabilidadPorMarcaAsync(items, cancellationToken),
            AgrupacionRentabilidadEnum.Articulo => await RentabilidadPorArticuloAsync(items, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(filtro), filtro.AgruparPor, "La agrupación de rentabilidad no es válida.")
        };
    }

    public async Task<ResumenInventarioFila> ObtenerResumenInventarioAsync(
        FiltroInventarioDto filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var resumen = await ArticulosFisicos(filtro)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Considerados = g.Count(),
                BajoMinimo = g.Sum(a => a.StockActual <= a.StockMinimo ? 1 : 0),
                Costo = g.Sum(a => a.StockActual * a.CostoReposicion),
                Venta = g.Sum(a => a.StockActual * a.PrecioVenta)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return resumen is null
            ? new ResumenInventarioFila(0, 0, 0m, 0m)
            : new ResumenInventarioFila(resumen.Considerados, resumen.BajoMinimo, resumen.Costo, resumen.Venta);
    }

    public async Task<IReadOnlyList<StockCriticoFila>> ObtenerStockCriticoAsync(
        FiltroInventarioDto filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var filas = await ArticulosFisicos(filtro)
            .Where(a => a.StockActual <= a.StockMinimo)
            .OrderByDescending(a => a.StockMinimo - a.StockActual)
            .ThenBy(a => a.Descripcion)
            .Select(a => new
            {
                a.Id,
                a.Descripcion,
                a.CodigoBarras,
                Categoria = a.Categoria == null ? null : a.Categoria.NombreCategoria,
                Marca = a.Marca == null ? null : a.Marca.NombreMarca,
                a.StockActual,
                a.StockMinimo,
                a.CostoReposicion,
                a.PrecioVenta
            })
            .ToListAsync(cancellationToken);

        return filas
            .Select(a => new StockCriticoFila(
                a.Id, a.Descripcion, a.CodigoBarras, a.Categoria, a.Marca, a.StockActual, a.StockMinimo, a.CostoReposicion, a.PrecioVenta))
            .ToList();
    }

    public async Task<IReadOnlyList<SinMovimientoFila>> ObtenerSinMovimientoAsync(
        FiltroInventarioDto filtro,
        DateTime sinVentasDesdeUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Ítems de ventas no borradas. La última venta de cada artículo sale de un LEFT JOIN con una sola agrupación
        // (MAX) y el corte se aplica sobre el resultado agrupado (HAVING): más rápido que una subconsulta por artículo.
        var itemsDeVentas = from d in _context.DetallesVenta.IgnoreQueryFilters()
                            join v in _context.Ventas.IgnoreQueryFilters() on d.IdVenta equals v.Id
                            where d.DeletedAt == null && v.DeletedAt == null
                            select new { d.IdArticulo, v.FechaHora };

        var porArticulo = from a in ArticulosFisicos(filtro).Where(a => a.StockActual > 0)
                          join x in itemsDeVentas on a.Id equals x.IdArticulo into itemsDelArticulo
                          from x in itemsDelArticulo.DefaultIfEmpty()
                          group x by new
                          {
                              a.Id,
                              a.Descripcion,
                              a.CodigoBarras,
                              Categoria = a.Categoria == null ? null : a.Categoria.NombreCategoria,
                              Marca = a.Marca == null ? null : a.Marca.NombreMarca,
                              a.StockActual,
                              a.CostoReposicion
                          } into g
                          select new
                          {
                              g.Key.Id,
                              g.Key.Descripcion,
                              g.Key.CodigoBarras,
                              g.Key.Categoria,
                              g.Key.Marca,
                              g.Key.StockActual,
                              g.Key.CostoReposicion,
                              Ultima = g.Max(x => (DateTime?)x.FechaHora)
                          };

        var filas = await porArticulo
            .Where(f => f.Ultima == null || f.Ultima < sinVentasDesdeUtc)
            .OrderByDescending(f => f.StockActual * f.CostoReposicion)
            .ThenBy(f => f.Descripcion)
            .ToListAsync(cancellationToken);

        return filas
            .Select(a => new SinMovimientoFila(
                a.Id, a.Descripcion, a.CodigoBarras, a.Categoria, a.Marca, a.StockActual, a.CostoReposicion, a.Ultima))
            .ToList();
    }

    /// <summary>
    /// Vista de los ítems vendidos en el rango (D-7, D-8): cada ítem con su ingreso neto y su costo histórico.
    /// Las ventas de un artículo dado de baja siguen contando (R-1, RF-06).
    /// </summary>
    /// <remarks>
    /// Usa <c>IgnoreQueryFilters</c> y filtra A MANO la baja lógica de la venta y del ítem. Es la única excepción
    /// justificada a la Ley 2 (no filtrar a mano): el filtro global también ocultaría la fila del artículo dado de
    /// baja y, con el join interno, descartaría sus ventas. Por eso el artículo es la única tabla que NO se filtra.
    /// El ingreso neto prorratea el descuento global según el peso del ítem en el subtotal (D-4).
    /// </remarks>
    internal IQueryable<ItemVendido> ItemsVendidos(RangoUtcDto rango)
    {
        DateTime desde = rango.DesdeUtc;
        DateTime hasta = rango.HastaExclusivoUtc;

        return from d in _context.DetallesVenta.IgnoreQueryFilters().AsNoTracking()
               join v in _context.Ventas.IgnoreQueryFilters() on d.IdVenta equals v.Id
               join a in _context.Articulos.IgnoreQueryFilters() on d.IdArticulo equals a.Id
               where v.DeletedAt == null
                     && d.DeletedAt == null
                     && v.FechaHora >= desde
                     && v.FechaHora < hasta
               select new ItemVendido
               {
                   IdVenta = v.Id,
                   IdUsuario = v.IdUsuario,
                   FechaHora = v.FechaHora,
                   IdArticulo = a.Id,
                   IdCategoria = a.IdCategoria,
                   IdMarca = a.IdMarca,
                   Cantidad = d.Cantidad,
                   IngresoNeto = d.SubtotalItem - (v.Subtotal == 0m ? 0m : v.Descuento * d.SubtotalItem / v.Subtotal),
                   Costo = d.CostoUnitario * d.Cantidad
               };
    }

    /// <summary>Ítems vendidos del rango ya filtrados por categoría y marca, listos para agrupar la rentabilidad.</summary>
    internal IQueryable<ItemVendido> RentabilidadItems(RangoUtcDto rango, FiltroRentabilidadDto filtro)
    {
        return FiltrarItems(ItemsVendidos(rango), filtro.IdCategoria, filtro.IdMarca);
    }

    /// <summary>Ventas no borradas del rango (misma excepción a la Ley 2 que <see cref="ItemsVendidos"/>).</summary>
    internal IQueryable<Venta> VentasDelRango(RangoUtcDto rango)
    {
        DateTime desde = rango.DesdeUtc;
        DateTime hasta = rango.HastaExclusivoUtc;

        return _context.Ventas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => v.DeletedAt == null && v.FechaHora >= desde && v.FechaHora < hasta);
    }

    /// <summary>Consulta SQL de las ventas por semana, expuesta a los tests para verificar que se resuelve en el servidor.</summary>
    internal string ConsultaVentasPorSemana(RangoUtcDto rango)
    {
        DateTime ancla = AnclaLunes;
        int desfase = rango.DesfaseMinutos;

        return VentasDelRango(rango)
            .GroupBy(v => EF.Functions.DateDiffDay(ancla, v.FechaHora.AddMinutes(desfase)) / 7)
            .Select(g => new { Semana = g.Key, Cantidad = g.Count(), Total = g.Sum(v => v.Total) })
            .ToQueryString();
    }

    private static IQueryable<ItemVendido> FiltrarItems(IQueryable<ItemVendido> items, int? idCategoria, int? idMarca)
    {
        if (idCategoria.HasValue)
        {
            items = items.Where(i => i.IdCategoria == idCategoria.Value);
        }

        if (idMarca.HasValue)
        {
            items = items.Where(i => i.IdMarca == idMarca.Value);
        }

        return items;
    }

    /// <summary>
    /// Artículos vigentes que ocupan lugar en el inventario (no servicios), filtrados por categoría y marca. La baja
    /// lógica se filtra a mano porque <see cref="ObtenerSinMovimientoAsync"/> combina esta consulta con una subconsulta
    /// que usa <c>IgnoreQueryFilters</c>, y eso desactiva los filtros globales de toda la consulta.
    /// </summary>
    private IQueryable<Articulo> ArticulosFisicos(FiltroInventarioDto filtro)
    {
        IQueryable<Articulo> articulos = _context.Articulos.AsNoTracking().Where(a => a.DeletedAt == null && !a.EsServicio);

        if (filtro.IdCategoria.HasValue)
        {
            articulos = articulos.Where(a => a.IdCategoria == filtro.IdCategoria.Value);
        }

        if (filtro.IdMarca.HasValue)
        {
            articulos = articulos.Where(a => a.IdMarca == filtro.IdMarca.Value);
        }

        return articulos;
    }

    private static async Task<IReadOnlyList<VentasPeriodoFila>> AgruparPorDiaAsync(
        IQueryable<Venta> ventas, int desfaseMinutos, CancellationToken cancellationToken)
    {
        var filas = await ventas
            .GroupBy(v => v.FechaHora.AddMinutes(desfaseMinutos).Date)
            .Select(g => new
            {
                Dia = g.Key,
                Cantidad = g.Count(),
                Total = g.Sum(v => v.Total),
                Descuentos = g.Sum(v => v.Descuento)
            })
            .OrderBy(x => x.Dia)
            .ToListAsync(cancellationToken);

        return filas
            .Select(x => new VentasPeriodoFila(DateOnly.FromDateTime(x.Dia), x.Cantidad, x.Total, x.Descuentos))
            .ToList();
    }

    private static async Task<IReadOnlyList<VentasPeriodoFila>> AgruparPorSemanaAsync(
        IQueryable<Venta> ventas, int desfaseMinutos, CancellationToken cancellationToken)
    {
        DateTime ancla = AnclaLunes;

        var filas = await ventas
            .GroupBy(v => EF.Functions.DateDiffDay(ancla, v.FechaHora.AddMinutes(desfaseMinutos)) / 7)
            .Select(g => new
            {
                Semana = g.Key,
                Cantidad = g.Count(),
                Total = g.Sum(v => v.Total),
                Descuentos = g.Sum(v => v.Descuento)
            })
            .OrderBy(x => x.Semana)
            .ToListAsync(cancellationToken);

        // El lunes de cada semana se calcula sobre las filas ya agregadas (pocas), no sobre las ventas.
        return filas
            .Select(x => new VentasPeriodoFila(
                DateOnly.FromDateTime(AnclaLunes.AddDays(x.Semana * 7)), x.Cantidad, x.Total, x.Descuentos))
            .ToList();
    }

    private static async Task<IReadOnlyList<VentasPeriodoFila>> AgruparPorMesAsync(
        IQueryable<Venta> ventas, int desfaseMinutos, CancellationToken cancellationToken)
    {
        var filas = await ventas
            .GroupBy(v => new
            {
                v.FechaHora.AddMinutes(desfaseMinutos).Year,
                v.FechaHora.AddMinutes(desfaseMinutos).Month
            })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Cantidad = g.Count(),
                Total = g.Sum(v => v.Total),
                Descuentos = g.Sum(v => v.Descuento)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        return filas
            .Select(x => new VentasPeriodoFila(new DateOnly(x.Year, x.Month, 1), x.Cantidad, x.Total, x.Descuentos))
            .ToList();
    }

    private async Task<IReadOnlyList<RentabilidadFila>> RentabilidadPorCategoriaAsync(
        IQueryable<ItemVendido> items, CancellationToken cancellationToken)
    {
        var grupos = await items
            .GroupBy(i => i.IdCategoria)
            .Select(g => new
            {
                Id = g.Key,
                Unidades = g.Sum(i => i.Cantidad),
                Ingreso = g.Sum(i => i.IngresoNeto),
                Costo = g.Sum(i => i.Costo)
            })
            .ToListAsync(cancellationToken);

        var ids = grupos.Where(g => g.Id.HasValue).Select(g => g.Id!.Value).ToList();
        var nombres = await _context.Categorias
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NombreCategoria, cancellationToken);

        return ArmarFilas(grupos.Select(g => (g.Id, g.Id.HasValue ? nombres[g.Id.Value] : null, g.Unidades, g.Ingreso, g.Costo)));
    }

    private async Task<IReadOnlyList<RentabilidadFila>> RentabilidadPorMarcaAsync(
        IQueryable<ItemVendido> items, CancellationToken cancellationToken)
    {
        var grupos = await items
            .GroupBy(i => i.IdMarca)
            .Select(g => new
            {
                Id = g.Key,
                Unidades = g.Sum(i => i.Cantidad),
                Ingreso = g.Sum(i => i.IngresoNeto),
                Costo = g.Sum(i => i.Costo)
            })
            .ToListAsync(cancellationToken);

        var ids = grupos.Where(g => g.Id.HasValue).Select(g => g.Id!.Value).ToList();
        var nombres = await _context.Marcas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.NombreMarca, cancellationToken);

        return ArmarFilas(grupos.Select(g => (g.Id, g.Id.HasValue ? nombres[g.Id.Value] : null, g.Unidades, g.Ingreso, g.Costo)));
    }

    private async Task<IReadOnlyList<RentabilidadFila>> RentabilidadPorArticuloAsync(
        IQueryable<ItemVendido> items, CancellationToken cancellationToken)
    {
        var grupos = await items
            .GroupBy(i => i.IdArticulo)
            .Select(g => new
            {
                Id = g.Key,
                Unidades = g.Sum(i => i.Cantidad),
                Ingreso = g.Sum(i => i.IngresoNeto),
                Costo = g.Sum(i => i.Costo)
            })
            .ToListAsync(cancellationToken);

        var ids = grupos.Select(g => g.Id).ToList();
        var nombres = await _context.Articulos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Descripcion, cancellationToken);

        return ArmarFilas(grupos.Select(g => ((int?)g.Id, (string?)nombres[g.Id], g.Unidades, g.Ingreso, g.Costo)));
    }

    private static List<RentabilidadFila> ArmarFilas(IEnumerable<(int? Id, string? Nombre, int Unidades, decimal Ingreso, decimal Costo)> grupos)
    {
        return grupos
            .OrderBy(g => g.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new RentabilidadFila(g.Id, g.Nombre, g.Unidades, g.Ingreso, g.Costo))
            .ToList();
    }

    private async Task<Dictionary<int, DatosArticulo>> DatosDeArticulosAsync(List<int> ids, CancellationToken cancellationToken)
    {
        return await _context.Articulos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new DatosArticulo(
                a.Id,
                a.Descripcion,
                a.CodigoBarras,
                a.Categoria == null ? null : a.Categoria.NombreCategoria,
                a.Marca == null ? null : a.Marca.NombreMarca))
            .ToDictionaryAsync(a => a.IdArticulo, cancellationToken);
    }

    private sealed record DatosArticulo(int IdArticulo, string Descripcion, string? CodigoBarras, string? Categoria, string? Marca);
}
