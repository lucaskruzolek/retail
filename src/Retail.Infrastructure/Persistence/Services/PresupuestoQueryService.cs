using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Presupuestos;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de solo lectura sobre presupuestos (RF-11, RF-12): filtros, búsqueda, orden y paginación se evalúan en
/// SQL Server y se proyectan directamente a DTOs, sin Change Tracker (Ley 8).
/// </summary>
public class PresupuestoQueryService : IPresupuestoQueryService
{
    private readonly RetailDbContext _context;

    public PresupuestoQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PresupuestosPaginadosDto> ListarAsync(
        ConsultaPresupuestosDto consulta,
        DateOnly hoy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        IQueryable<Presupuesto> query = PresupuestosConSusReferencias();

        query = AplicarFiltros(query, consulta, hoy);

        if (!string.IsNullOrWhiteSpace(consulta.TerminoBusqueda))
        {
            var termino = consulta.TerminoBusqueda.Trim();
            var patron = $"%{termino}%";

            // Un entero también se busca como número de presupuesto. Si no es un número, idBuscado queda en 0 y nunca
            // coincide: los identificadores empiezan en 1.
            var idBuscado = int.TryParse(termino, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? numero : 0;

            // Las columnas titulo y razón social tienen collation insensible a acentos y mayúsculas, así que el LIKE
            // encuentra "lapiz" aunque el título diga "Lápiz".
            query = query.Where(p =>
                p.Id == idBuscado ||
                (p.Titulo != null && EF.Functions.Like(p.Titulo, patron)) ||
                (p.Cliente != null && EF.Functions.Like(p.Cliente.RazonSocialONombre, patron)));
        }

        var totalRegistros = await query.CountAsync(cancellationToken);

        var tamanoPagina = Math.Max(1, consulta.TamanoPagina);
        var pagina = Math.Max(1, consulta.Pagina);

        var items = await query
            .OrderByDescending(p => p.FechaEmision)
            .ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(p => new PresupuestoResumenDto
            {
                IdPresupuesto = p.Id,
                Titulo = p.Titulo,
                FechaEmision = p.FechaEmision,
                FechaVencimiento = p.FechaVencimiento,
                ClienteNombre = p.Cliente != null ? p.Cliente.RazonSocialONombre : null,
                UsuarioNombre = p.Usuario != null ? p.Usuario.Nombre + " " + p.Usuario.Apellido : string.Empty,
                Total = p.Total,
                Estado = p.Estado,
                EstaVigente = p.FechaVencimiento >= hoy,
                CantidadRenglones = p.Detalles.Count,
                IdPresupuestoReemplazado = p.IdPresupuestoReemplazado
            })
            .ToListAsync(cancellationToken);

        // El reemplazante se resuelve solo para la página y con una consulta aparte (R-3): evita una subconsulta
        // correlacionada por cada fila de todo el listado filtrado.
        var idsReemplazados = items
            .Where(i => i.Estado == EstadoPresupuestoEnum.Reemplazado)
            .Select(i => i.IdPresupuesto)
            .ToList();

        if (idsReemplazados.Count > 0)
        {
            var reemplazantes = await _context.Presupuestos
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.DeletedAt == null
                    && r.IdPresupuestoReemplazado != null
                    && idsReemplazados.Contains(r.IdPresupuestoReemplazado.Value))
                .Select(r => new { Reemplazado = r.IdPresupuestoReemplazado!.Value, r.Id })
                .ToDictionaryAsync(r => r.Reemplazado, r => r.Id, cancellationToken);

            items = items
                .Select(i => reemplazantes.TryGetValue(i.IdPresupuesto, out var idReemplazante)
                    ? i with { IdPresupuestoReemplazante = idReemplazante }
                    : i)
                .ToList();
        }

        return new PresupuestosPaginadosDto
        {
            Items = items,
            TotalRegistros = totalRegistros,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina
        };
    }

    public async Task<PresupuestoDto?> ObtenerAsync(
        int idPresupuesto,
        DateOnly hoy,
        CancellationToken cancellationToken = default)
    {
        return await PresupuestosConSusReferencias()
            .Where(p => p.Id == idPresupuesto)
            .Select(p => new PresupuestoDto
            {
                IdPresupuesto = p.Id,
                IdUsuario = p.IdUsuario,
                UsuarioNombre = p.Usuario != null ? p.Usuario.Nombre + " " + p.Usuario.Apellido : null,
                IdCliente = p.IdCliente,
                ClienteNombre = p.Cliente != null ? p.Cliente.RazonSocialONombre : null,
                Titulo = p.Titulo,
                FechaEmision = p.FechaEmision,
                FechaVencimiento = p.FechaVencimiento,
                Subtotal = p.Subtotal,
                Descuento = p.Descuento,
                Total = p.Total,
                Estado = p.Estado,
                EstaVigente = p.FechaVencimiento >= hoy,
                IdPresupuestoReemplazado = p.IdPresupuestoReemplazado,
                IdPresupuestoReemplazante = _context.Presupuestos
                    .IgnoreQueryFilters()
                    .Where(r => r.DeletedAt == null && r.IdPresupuestoReemplazado == p.Id)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefault(),
                Items = p.Detalles
                    .OrderBy(d => d.Id)
                    .Select(d => new DetallePresupuestoDto
                    {
                        IdArticulo = d.IdArticulo,
                        CodigoBarras = d.Articulo != null ? d.Articulo.CodigoBarras : null,
                        Descripcion = d.Descripcion,
                        Cantidad = d.Cantidad,
                        PrecioUnitarioPactado = d.PrecioUnitarioPactado
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClienteConPresupuestosDto>> ListarClientesConPresupuestosAsync(
        CancellationToken cancellationToken = default)
    {
        // Se ignoran los filtros globales (como en PresupuestosConSusReferencias): un cliente dado de baja después de
        // emitir su presupuesto sigue siendo una opción, porque el presupuesto sigue mostrando su nombre.
        return await _context.Clientes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => _context.Presupuestos
                .IgnoreQueryFilters()
                .Any(p => p.DeletedAt == null && p.IdCliente == c.Id))
            .OrderBy(c => c.RazonSocialONombre)
            .ThenBy(c => c.Id)
            .Select(c => new ClienteConPresupuestosDto
            {
                IdCliente = c.Id,
                Nombre = c.RazonSocialONombre
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Filtros de columna: dentro de cada uno las opciones se combinan con O, y entre filtros con Y. Todo se traduce a
    /// SQL; una lista vacía o un extremo nulo no agrega condición.
    /// </summary>
    private static IQueryable<Presupuesto> AplicarFiltros(
        IQueryable<Presupuesto> query,
        ConsultaPresupuestosDto consulta,
        DateOnly hoy)
    {
        if (consulta.Estados.Count > 0)
        {
            // Vigentes y Vencidos son estados de consulta, no del dominio (PS-08): se derivan de la fecha de vencimiento.
            // Los estados elegidos se capturan como booleanos para componer un solo Where con O que EF traduce a SQL.
            var vigentes = consulta.Estados.Contains(FiltroEstadoPresupuestoEnum.Vigentes);
            var vencidos = consulta.Estados.Contains(FiltroEstadoPresupuestoEnum.Vencidos);
            var convertidos = consulta.Estados.Contains(FiltroEstadoPresupuestoEnum.Convertidos);
            var anulados = consulta.Estados.Contains(FiltroEstadoPresupuestoEnum.Anulados);
            var reemplazados = consulta.Estados.Contains(FiltroEstadoPresupuestoEnum.Reemplazados);

            query = query.Where(p =>
                (vigentes && p.Estado == EstadoPresupuestoEnum.Pendiente && p.FechaVencimiento >= hoy) ||
                (vencidos && p.Estado == EstadoPresupuestoEnum.Pendiente && p.FechaVencimiento < hoy) ||
                (convertidos && p.Estado == EstadoPresupuestoEnum.Convertido) ||
                (anulados && p.Estado == EstadoPresupuestoEnum.Anulado) ||
                (reemplazados && p.Estado == EstadoPresupuestoEnum.Reemplazado));
        }

        if (consulta.IdsCliente.Count > 0 || consulta.IncluirConsumidorFinal)
        {
            var idsCliente = consulta.IdsCliente.ToList();
            var incluirConsumidorFinal = consulta.IncluirConsumidorFinal;

            query = query.Where(p =>
                (p.IdCliente != null && idsCliente.Contains(p.IdCliente.Value)) ||
                (incluirConsumidorFinal && p.IdCliente == null));
        }

        if (consulta.EmisionDesdeUtc is { } emisionDesde)
        {
            query = query.Where(p => p.FechaEmision >= emisionDesde);
        }

        if (consulta.EmisionHastaUtc is { } emisionHasta)
        {
            query = query.Where(p => p.FechaEmision < emisionHasta);
        }

        if (consulta.VenceDesde is { } venceDesde)
        {
            query = query.Where(p => p.FechaVencimiento >= venceDesde);
        }

        if (consulta.VenceHasta is { } venceHasta)
        {
            query = query.Where(p => p.FechaVencimiento <= venceHasta);
        }

        return query;
    }

    /// <summary>
    /// Presupuestos no borrados, sin aplicar el filtro global de borrado lógico a sus referencias. El filtro global
    /// también actúa sobre las navegaciones: un cliente dado de baja después de emitir aparecería como Consumidor
    /// Final, y un documento ya entregado debe seguir diciendo a quién se le cotizó (Ley 3: la baja lógica conserva el
    /// historial). Por eso se ignoran los filtros y se excluyen a mano solo los presupuestos borrados.
    /// </summary>
    private IQueryable<Presupuesto> PresupuestosConSusReferencias()
    {
        return _context.Presupuestos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.DeletedAt == null);
    }
}
