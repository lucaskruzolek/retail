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
        IQueryable<CatalogoProveedor> query = _context.CatalogosProveedores
            .Include(c => c.Proveedor)
            .AsNoTracking();

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

        // Recuperar artículos vinculados de forma proyectada y ligera para enriquecer la grilla
        var articulosVinculados = await _context.Articulos
            .AsNoTracking()
            .Where(a => a.IdCatalogoProveedor != null)
            .Select(a => new
            {
                a.Id,
                IdCatalogoProveedor = a.IdCatalogoProveedor!.Value,
                a.Descripcion,
                a.PrecioVenta,
                a.StockActual
            })
            .ToListAsync(cancellationToken);

        var articulosMap = articulosVinculados.ToDictionary(a => a.IdCatalogoProveedor, a => a);

        // Recuperar códigos de barra de artículos propios no vinculados para detección inteligente (RF-05)
        var codigosBarrasTiendaLista = await _context.Articulos
            .AsNoTracking()
            .Where(a => a.CodigoBarras != null && a.CodigoBarras != "" && a.IdCatalogoProveedor == null)
            .Select(a => a.CodigoBarras!)
            .ToListAsync(cancellationToken);

        var codigosBarrasTienda = codigosBarrasTiendaLista
            .Where(cb => !string.IsNullOrWhiteSpace(cb))
            .Select(cb => cb.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (consulta.EstadoVinculacion == EstadoVinculacionCatalogoEnum.SinIncorporar)
        {
            var vinculadosIds = articulosVinculados.Select(a => a.IdCatalogoProveedor).ToHashSet();
            query = query.Where(c => !vinculadosIds.Contains(c.Id));
        }
        else if (consulta.EstadoVinculacion == EstadoVinculacionCatalogoEnum.YaEnTienda)
        {
            var vinculadosIds = articulosVinculados.Select(a => a.IdCatalogoProveedor).ToHashSet();
            query = query.Where(c => vinculadosIds.Contains(c.Id));
        }

        int totalRegistros = await query.CountAsync(cancellationToken);

        int tamanoPagina = Math.Max(1, consulta.TamañoPagina);
        int pagina = Math.Max(1, consulta.Pagina);

        var entidades = await query
            .OrderBy(c => c.DescripcionProveedor)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);

        var dtos = entidades.Select(c =>
        {
            var estaVinculado = articulosMap.TryGetValue(c.Id, out var art);
            var coincidePorCodigoBarras = !estaVinculado &&
                !string.IsNullOrWhiteSpace(c.CodigoBarras) &&
                codigosBarrasTienda.Contains(c.CodigoBarras.Trim());

            return new CatalogoProveedorDto
            {
                Id = c.Id,
                IdProveedor = c.IdProveedor,
                ProveedorRazonSocial = c.Proveedor?.RazonSocial ?? string.Empty,
                CodigoProveedor = c.CodigoProveedor,
                CodigoBarras = c.CodigoBarras,
                DescripcionProveedor = c.DescripcionProveedor,
                CostoReposicion = c.CostoReposicion,
                FechaActualizacion = c.FechaActualizacion,
                EstaVinculado = estaVinculado,
                CoincideConArticuloTienda = coincidePorCodigoBarras,
                IdArticuloVinculado = art?.Id,
                NombreArticuloTienda = art?.Descripcion,
                PrecioVentaTienda = art?.PrecioVenta,
                StockActualTienda = art?.StockActual
            };
        }).ToList();

        return new CatalogoPaginadoDto
        {
            Items = dtos,
            TotalRegistros = totalRegistros,
            PaginaActual = pagina,
            TamanoPagina = tamanoPagina
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
