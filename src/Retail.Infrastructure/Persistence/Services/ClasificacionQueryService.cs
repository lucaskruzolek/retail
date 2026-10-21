using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Application.Interfaces.Persistence;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de lectura de rubros, categorías y marcas (Track 6.5, D-20). Todo filtro, orden y conteo se ejecuta
/// en SQL Server con proyecciones <c>AsNoTracking</c> (Ley 8). La colación <c>Modern_Spanish_CI_AI</c> de las
/// columnas de nombre hace que <c>LIKE</c> ignore mayúsculas y acentos, sin normalizar texto en C#.
/// </summary>
public class ClasificacionQueryService : IClasificacionQueryService
{
    private readonly RetailDbContext _context;

    public ClasificacionQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<RubroDto>> ListarRubrosAsync(string? filtro, CancellationToken cancellationToken = default)
    {
        var query = _context.Rubros.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var patron = $"%{filtro.Trim()}%";
            query = query.Where(r => EF.Functions.Like(r.NombreRubro, patron));
        }

        // El filtro global de borrado lógico rige tanto para los rubros como para las categorías contadas, así que
        // CantidadEnUso incluye únicamente las categorías activas.
        return await query
            .OrderBy(r => r.NombreRubro)
            .Select(r => new RubroDto
            {
                IdRubro = r.Id,
                NombreRubro = r.NombreRubro,
                CantidadEnUso = _context.Categorias.Count(c => c.IdRubro == r.Id)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(string? filtro, int? idRubro, CancellationToken cancellationToken = default)
    {
        // R-1 + comportamiento de EF Core 8: IgnoreQueryFilters() afecta a TODA la consulta, no a una subconsulta.
        // Como el conteo debe incluir los artículos dados de baja, se ignoran los filtros globales y las
        // categorías y rubros activos se filtran de forma explícita con DeletedAt == null.
        var query = _context.Categorias
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var patron = $"%{filtro.Trim()}%";
            query = query.Where(c => EF.Functions.Like(c.NombreCategoria, patron));
        }

        // D-26: el filtro por rubro se compone sobre el IQueryable y se resuelve en SQL (Ley 8)
        if (idRubro.HasValue)
        {
            query = query.Where(c => c.IdRubro == idRubro.Value);
        }

        return await query
            .OrderBy(c => c.NombreCategoria)
            .Select(c => new CategoriaDto
            {
                IdCategoria = c.Id,
                NombreCategoria = c.NombreCategoria,
                IdRubro = c.IdRubro,
                NombreRubro = _context.Rubros
                    .Where(r => r.Id == c.IdRubro && r.DeletedAt == null)
                    .Select(r => r.NombreRubro)
                    .FirstOrDefault(),
                CantidadEnUso = _context.Articulos.Count(a => a.IdCategoria == c.Id)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarcaDto>> ListarMarcasAsync(string? filtro, CancellationToken cancellationToken = default)
    {
        // Misma decisión que en ListarCategoriasAsync: filtros globales ignorados para contar también las bajas (R-1)
        // y marcas activas filtradas de forma explícita.
        var query = _context.Marcas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var patron = $"%{filtro.Trim()}%";
            query = query.Where(m => EF.Functions.Like(m.NombreMarca, patron));
        }

        return await query
            .OrderBy(m => m.NombreMarca)
            .Select(m => new MarcaDto
            {
                IdMarca = m.Id,
                NombreMarca = m.NombreMarca,
                CantidadEnUso = _context.Articulos.Count(a => a.IdMarca == m.Id)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarCategoriasActivasPorRubroAsync(int idRubro, CancellationToken cancellationToken = default)
    {
        return await _context.Categorias.CountAsync(c => c.IdRubro == idRubro, cancellationToken);
    }

    public async Task<int> ContarArticulosPorCategoriaAsync(int idCategoria, CancellationToken cancellationToken = default)
    {
        return await _context.Articulos
            .IgnoreQueryFilters()
            .CountAsync(a => a.IdCategoria == idCategoria, cancellationToken);
    }

    public async Task<int> ContarArticulosPorMarcaAsync(int idMarca, CancellationToken cancellationToken = default)
    {
        return await _context.Articulos
            .IgnoreQueryFilters()
            .CountAsync(a => a.IdMarca == idMarca, cancellationToken);
    }
}
