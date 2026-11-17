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

        // Vigentes y Vencidos son estados de consulta, no del dominio (PS-08): se derivan de la fecha de vencimiento.
        query = consulta.Filtro switch
        {
            FiltroEstadoPresupuestoEnum.Vigentes => query.Where(p =>
                p.Estado == EstadoPresupuestoEnum.Pendiente && p.FechaVencimiento >= hoy),
            FiltroEstadoPresupuestoEnum.Vencidos => query.Where(p =>
                p.Estado == EstadoPresupuestoEnum.Pendiente && p.FechaVencimiento < hoy),
            FiltroEstadoPresupuestoEnum.Convertidos => query.Where(p => p.Estado == EstadoPresupuestoEnum.Convertido),
            FiltroEstadoPresupuestoEnum.Anulados => query.Where(p => p.Estado == EstadoPresupuestoEnum.Anulado),
            _ => query
        };

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
                CantidadRenglones = p.Detalles.Count
            })
            .ToListAsync(cancellationToken);

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
