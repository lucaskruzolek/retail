using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Pedidos;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de solo lectura sobre pedidos a proveedor, con filtrado, orden, paginación y agregación delegados a
/// SQL Server (Ley 8).
/// </summary>
public class PedidoQueryService : IPedidoQueryService
{
    private readonly RetailDbContext _context;

    public PedidoQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyDictionary<int, int>> ObtenerArticulosVinculadosAsync(
        IReadOnlyCollection<int> idsCatalogoProveedor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idsCatalogoProveedor);

        // Con fuentes múltiples (L3, J6) el artículo de un ítem es el de CATALOGOS_PROVEEDORES.id_articulo: una fuente
        // adicional también se puede pedir. La subconsulta aplica el filtro global, así un artículo dado de baja no
        // cuenta aunque el ítem todavía lo apunte. Cada ítem es una fila, así que la clave del diccionario no se repite.
        var ids = idsCatalogoProveedor.Distinct().ToList();

        return await _context.CatalogosProveedores
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) && _context.Articulos.Any(a => a.Id == c.IdArticulo))
            .Select(c => new { c.Id, IdArticulo = c.IdArticulo!.Value })
            .ToDictionaryAsync(c => c.Id, c => c.IdArticulo, cancellationToken);
    }

    public async Task<VistaPreviaPedidoDto> ObtenerVistaPreviaAsync(
        IReadOnlyCollection<int> idsCatalogoProveedor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idsCatalogoProveedor);

        var ids = idsCatalogoProveedor.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new VistaPreviaPedidoDto();
        }

        var items = await _context.CatalogosProveedores
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.IdProveedor,
                RazonSocialProveedor = c.Proveedor!.RazonSocial,
                c.CodigoProveedor,
                c.DescripcionProveedor,
                c.CostoReposicion
            })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        // La resolución ítem → artículo se reutiliza para que siga habiendo un solo punto que cambia en L3.
        var articulosPorItem = await ObtenerArticulosVinculadosAsync(items.Keys.ToList(), cancellationToken);
        var idsArticulos = articulosPorItem.Values.ToList();
        var stocks = await _context.Articulos
            .AsNoTracking()
            .Where(a => idsArticulos.Contains(a.Id))
            .Select(a => new { a.Id, a.StockActual, a.StockMinimo })
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var idsProveedores = items.Values
            .Where(i => articulosPorItem.ContainsKey(i.Id))
            .Select(i => i.IdProveedor)
            .Distinct()
            .ToList();
        var borradores = await _context.Pedidos
            .AsNoTracking()
            .Where(p => idsProveedores.Contains(p.IdProveedor) && p.Estado == EstadoPedidoEnum.Borrador)
            .Select(p => new { p.Id, p.IdProveedor, CantidadRenglones = p.Detalles.Count })
            .ToDictionaryAsync(p => p.IdProveedor, cancellationToken);

        // Al agregar, la cantidad se suma a la que ya tiene el borrador: el modal la muestra para no pedir de más (H3).
        // El filtro global oculta los renglones quitados (D4).
        var idsBorradores = borradores.Values.Select(b => b.Id).ToList();
        var cantidadesEnBorrador = await _context.Set<DetallePedido>()
            .AsNoTracking()
            .Where(d => idsBorradores.Contains(d.IdPedido) && ids.Contains(d.IdCatalogoProveedor))
            .Select(d => new { d.IdCatalogoProveedor, d.Cantidad })
            .ToDictionaryAsync(d => d.IdCatalogoProveedor, d => d.Cantidad, cancellationToken);

        var renglones = new List<RenglonVistaPreviaDto>();
        var omitidos = new List<RenglonOmitidoDto>();
        foreach (var id in ids)
        {
            if (!items.TryGetValue(id, out var item))
            {
                omitidos.Add(new RenglonOmitidoDto { IdCatalogoProveedor = id, Motivo = MotivoOmisionPedidoEnum.ItemInexistente });
            }
            // El segundo TryGetValue cubre un artículo dado de baja entre las dos consultas: queda sin vincular.
            else if (!articulosPorItem.TryGetValue(id, out var idArticulo) || !stocks.TryGetValue(idArticulo, out var stock))
            {
                omitidos.Add(new RenglonOmitidoDto
                {
                    IdCatalogoProveedor = id,
                    CodigoProveedor = item.CodigoProveedor,
                    Descripcion = item.DescripcionProveedor,
                    Motivo = MotivoOmisionPedidoEnum.SinIncorporar
                });
            }
            else
            {
                renglones.Add(new RenglonVistaPreviaDto
                {
                    IdCatalogoProveedor = id,
                    IdProveedor = item.IdProveedor,
                    CodigoProveedor = item.CodigoProveedor,
                    Descripcion = item.DescripcionProveedor,
                    CostoActual = item.CostoReposicion,
                    IdArticulo = idArticulo,
                    StockActual = stock.StockActual,
                    StockMinimo = stock.StockMinimo,
                    CantidadEnBorrador = cantidadesEnBorrador.GetValueOrDefault(id)
                });
            }
        }

        var razonesSociales = items.Values
            .GroupBy(i => i.IdProveedor)
            .ToDictionary(g => g.Key, g => g.First().RazonSocialProveedor);
        var destinos = renglones
            .Select(r => r.IdProveedor)
            .Distinct()
            .Select(idProveedor =>
            {
                var borrador = borradores.GetValueOrDefault(idProveedor);
                return new DestinoPedidoDto
                {
                    IdProveedor = idProveedor,
                    RazonSocialProveedor = razonesSociales[idProveedor],
                    IdBorrador = borrador?.Id,
                    CantidadRenglonesBorrador = borrador?.CantidadRenglones ?? 0
                };
            })
            .OrderBy(d => d.RazonSocialProveedor)
            .ToList();

        return new VistaPreviaPedidoDto { Destinos = destinos, Renglones = renglones, Omitidos = omitidos };
    }

    public async Task<int?> ObtenerIdBorradorAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        return await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.IdProveedor == idProveedor && p.Estado == EstadoPedidoEnum.Borrador)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PedidoResumenDto?> ObtenerResumenAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        var fila = await ProyectarResumenes(_context.Pedidos.AsNoTracking().Where(p => p.Id == idPedido))
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null ? null : ConTotalRedondeado(fila);
    }

    public async Task<PedidosPaginadosDto> ListarAsync(ConsultaPedidosDto consulta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        IQueryable<Pedido> query = _context.Pedidos.AsNoTracking();

        if (consulta.Estado.HasValue)
        {
            query = query.Where(p => p.Estado == consulta.Estado.Value);
        }

        if (consulta.IdProveedor.HasValue)
        {
            query = query.Where(p => p.IdProveedor == consulta.IdProveedor.Value);
        }

        int totalRegistros = await query.CountAsync(cancellationToken);

        int tamanoPagina = Math.Max(1, consulta.TamanoPagina);
        int pagina = Math.Max(1, consulta.Pagina);

        var filas = await ProyectarResumenes(
                query
                    .OrderByDescending(p => p.CreatedAt)
                    .ThenByDescending(p => p.Id)
                    .Skip((pagina - 1) * tamanoPagina)
                    .Take(tamanoPagina))
            .ToListAsync(cancellationToken);

        return new PedidosPaginadosDto
        {
            Items = filas.Select(ConTotalRedondeado).ToList(),
            TotalRegistros = totalRegistros,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina
        };
    }

    public async Task<PedidoDetalleDto?> ObtenerDetalleAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        // El detalle debe mostrar también los renglones cuyo artículo fue dado de baja (F4), marcados como tales; por
        // eso se ignoran los filtros globales. Como IgnoreQueryFilters los desactiva todos, el borrado lógico de lo
        // que sí debe ocultarse (el pedido y sus renglones) se aplica a mano acá. Solo el artículo queda sin
        // filtrar, a propósito.
        var cabecera = await _context.Pedidos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Id == idPedido && p.DeletedAt == null)
            .Select(p => new
            {
                p.Id,
                p.IdProveedor,
                RazonSocialProveedor = p.Proveedor!.RazonSocial,
                p.Estado,
                p.CreatedAt,
                p.FechaEmision,
                p.FechaCierre,
                p.MotivoCancelacion
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        var filas = await _context.Set<DetallePedido>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => d.IdPedido == idPedido && d.DeletedAt == null)
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.IdCatalogoProveedor,
                d.IdArticulo,
                d.CodigoProveedor,
                d.Descripcion,
                d.Cantidad,
                d.CostoPactado,
                ArticuloDadoDeBaja = d.Articulo!.DeletedAt != null
            })
            .ToListAsync(cancellationToken);

        var renglones = filas
            .Select(f => new RenglonPedidoDto
            {
                IdCatalogoProveedor = f.IdCatalogoProveedor,
                IdArticulo = f.IdArticulo,
                CodigoProveedor = f.CodigoProveedor,
                Descripcion = f.Descripcion,
                Cantidad = f.Cantidad,
                CostoPactado = f.CostoPactado,
                Subtotal = ReglasMonetarias.Redondear(f.Cantidad * f.CostoPactado),
                ArticuloDadoDeBaja = f.ArticuloDadoDeBaja
            })
            .ToList();

        return new PedidoDetalleDto
        {
            IdPedido = cabecera.Id,
            IdProveedor = cabecera.IdProveedor,
            RazonSocialProveedor = cabecera.RazonSocialProveedor,
            Estado = cabecera.Estado,
            FechaCreacion = cabecera.CreatedAt,
            FechaEmision = cabecera.FechaEmision,
            FechaCierre = cabecera.FechaCierre,
            CantidadRenglones = renglones.Count,
            // Mismo criterio que Pedido.TotalEstimado: se suman los subtotales sin redondear y se redondea una vez (D-06).
            TotalEstimado = ReglasMonetarias.Redondear(filas.Sum(f => f.Cantidad * f.CostoPactado)),
            MotivoCancelacion = cabecera.MotivoCancelacion,
            Renglones = renglones
        };
    }

    public async Task<IReadOnlyList<PedidoEmitidoDto>> ListarEmitidosPorProveedorAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        var filas = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.IdProveedor == idProveedor && p.Estado == EstadoPedidoEnum.Emitido)
            .OrderByDescending(p => p.FechaEmision)
            .ThenByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                FechaEmision = p.FechaEmision!.Value,
                CantidadRenglones = p.Detalles.Count,
                Total = p.Detalles.Sum(d => (decimal?)(d.Cantidad * d.CostoPactado)) ?? 0m
            })
            .ToListAsync(cancellationToken);

        // Math.Round(AwayFromZero) no se traduce a SQL: se redondea en memoria sobre las filas ya traídas (F5).
        return filas
            .Select(f => new PedidoEmitidoDto
            {
                IdPedido = f.Id,
                FechaEmision = f.FechaEmision,
                CantidadRenglones = f.CantidadRenglones,
                TotalEstimado = ReglasMonetarias.Redondear(f.Total)
            })
            .ToList();
    }

    public async Task<PedidoParaCompraDto?> ObtenerParaCompraAsync(int idPedido, CancellationToken cancellationToken = default)
    {
        var cabecera = await _context.Pedidos
            .AsNoTracking()
            .Where(p => p.Id == idPedido && p.Estado == EstadoPedidoEnum.Emitido)
            .Select(p => new { p.Id, p.IdProveedor })
            .FirstOrDefaultAsync(cancellationToken);

        if (cabecera is null)
        {
            return null;
        }

        // No se puede comprar un artículo dado de baja (§5.2): su renglón se omite. La condición es explícita
        // (además del filtro global) para que la regla se lea en la propia consulta.
        var renglones = await _context.Set<DetallePedido>()
            .AsNoTracking()
            .Where(d => d.IdPedido == idPedido && d.Articulo!.DeletedAt == null)
            .OrderBy(d => d.Id)
            .Select(d => new RenglonPedidoParaCompraDto
            {
                IdArticulo = d.IdArticulo,
                CodigoBarras = d.Articulo!.CodigoBarras,
                DescripcionArticulo = d.Articulo.Descripcion,
                CodigoProveedor = d.CodigoProveedor,
                Cantidad = d.Cantidad,
                CostoPactado = d.CostoPactado
            })
            .ToListAsync(cancellationToken);

        return new PedidoParaCompraDto
        {
            IdPedido = cabecera.Id,
            IdProveedor = cabecera.IdProveedor,
            Renglones = renglones
        };
    }

    private static IQueryable<FilaResumenPedido> ProyectarResumenes(IQueryable<Pedido> pedidos)
    {
        return pedidos.Select(p => new FilaResumenPedido
        {
            IdPedido = p.Id,
            IdProveedor = p.IdProveedor,
            RazonSocialProveedor = p.Proveedor!.RazonSocial,
            Estado = p.Estado,
            FechaCreacion = p.CreatedAt,
            FechaEmision = p.FechaEmision,
            FechaCierre = p.FechaCierre,
            CantidadRenglones = p.Detalles.Count,
            Total = p.Detalles.Sum(d => (decimal?)(d.Cantidad * d.CostoPactado)) ?? 0m
        });
    }

    // Math.Round(AwayFromZero) no se traduce a SQL: la suma se hace en el servidor y el redondeo único (D-06) se
    // aplica en memoria sobre la página ya materializada (F5).
    private static PedidoResumenDto ConTotalRedondeado(FilaResumenPedido fila)
    {
        return new PedidoResumenDto
        {
            IdPedido = fila.IdPedido,
            IdProveedor = fila.IdProveedor,
            RazonSocialProveedor = fila.RazonSocialProveedor,
            Estado = fila.Estado,
            FechaCreacion = fila.FechaCreacion,
            FechaEmision = fila.FechaEmision,
            FechaCierre = fila.FechaCierre,
            CantidadRenglones = fila.CantidadRenglones,
            TotalEstimado = ReglasMonetarias.Redondear(fila.Total)
        };
    }

    private sealed class FilaResumenPedido
    {
        public int IdPedido { get; init; }

        public int IdProveedor { get; init; }

        public string RazonSocialProveedor { get; init; } = string.Empty;

        public EstadoPedidoEnum Estado { get; init; }

        public DateTime FechaCreacion { get; init; }

        public DateTime? FechaEmision { get; init; }

        public DateTime? FechaCierre { get; init; }

        public int CantidadRenglones { get; init; }

        public decimal Total { get; init; }
    }
}
