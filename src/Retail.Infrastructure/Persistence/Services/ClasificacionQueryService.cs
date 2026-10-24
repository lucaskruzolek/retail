using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Domain.Enums;
using Retail.Infrastructure.Persistence.Configurations;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Consultas de lectura de rubros, categorías y marcas (Track 6.5, D-20) y de los diccionarios del motor de
/// catalogación para su administración (Track 6.6, Fase 4b). Todo filtro, orden y conteo se ejecuta
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

    public async Task<IReadOnlyList<TerminoDto>> ListarTerminosAsync(
        string? filtro,
        int? idCategoria,
        bool soloAmbiguos,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TerminosClasificacion.AsNoTracking();

        // El término se guarda en forma de comparación: el filtro se lleva a la misma forma y la comparación es exacta.
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var patron = $"%{ReglasClasificacion.NormalizarTexto(filtro)}%";
            query = query.Where(t => EF.Functions.Like(t.Termino, patron));
        }

        if (idCategoria.HasValue)
        {
            query = query.Where(t => t.IdCategoria == idCategoria.Value);
        }

        if (soloAmbiguos)
        {
            query = query.Where(t => t.IdCategoria == null);
        }

        // Las subconsultas respetan el filtro global de borrado lógico de categorías y rubros.
        return await query
            .OrderBy(t => t.Termino)
            .Select(t => new TerminoDto
            {
                IdTermino = t.Id,
                Termino = t.Termino,
                IdCategoria = t.IdCategoria,
                NombreCategoria = _context.Categorias
                    .Where(c => c.Id == t.IdCategoria)
                    .Select(c => c.NombreCategoria)
                    .FirstOrDefault(),
                NombreRubro = _context.Categorias
                    .Where(c => c.Id == t.IdCategoria)
                    .SelectMany(c => _context.Rubros.Where(r => r.Id == c.IdRubro))
                    .Select(r => r.NombreRubro)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AliasListadoDto>> ListarAliasAsync(
        string? filtro,
        int? idMarca,
        CancellationToken cancellationToken = default)
    {
        // El join con Marcas (con su filtro global) deja afuera los alias de una marca dada de baja.
        var query = _context.Set<AliasMarca>()
            .AsNoTracking()
            .Join(
                _context.Marcas.AsNoTracking(),
                a => a.IdMarca,
                m => m.Id,
                (a, m) => new AliasListadoDto { IdMarca = m.Id, NombreMarca = m.NombreMarca, Alias = a.Alias });

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var patronAlias = $"%{ReglasClasificacion.NormalizarTexto(filtro)}%";
            var patronNombre = $"%{filtro.Trim()}%";
            query = query.Where(x => EF.Functions.Like(x.Alias, patronAlias) || EF.Functions.Like(x.NombreMarca, patronNombre));
        }

        if (idMarca.HasValue)
        {
            query = query.Where(x => x.IdMarca == idMarca.Value);
        }

        return await query
            .OrderBy(x => x.NombreMarca)
            .ThenBy(x => x.Alias)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrefijoListadoDto>> ListarPrefijosAsync(
        string? filtro,
        int? idMarca,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<PrefijoEanMarca>()
            .AsNoTracking()
            .Join(
                _context.Marcas.AsNoTracking(),
                p => p.IdMarca,
                m => m.Id,
                (p, m) => new PrefijoListadoDto { IdMarca = m.Id, NombreMarca = m.NombreMarca, Prefijo = p.Prefijo });

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var texto = filtro.Trim();
            var patronPrefijo = $"{texto}%";
            var patronNombre = $"%{texto}%";
            query = query.Where(x => EF.Functions.Like(x.Prefijo, patronPrefijo) || EF.Functions.Like(x.NombreMarca, patronNombre));
        }

        if (idMarca.HasValue)
        {
            query = query.Where(x => x.IdMarca == idMarca.Value);
        }

        return await query
            .OrderBy(x => x.NombreMarca)
            .ThenBy(x => x.Prefijo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EquivalenciaDto>> ListarEquivalenciasAsync(
        int idProveedor,
        TipoEquivalenciaEnum? tipo,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<EquivalenciaClasificacion>()
            .AsNoTracking()
            .Where(e => e.IdProveedor == idProveedor);

        if (tipo.HasValue)
        {
            query = query.Where(e => e.Tipo == tipo.Value);
        }

        return await query
            .OrderBy(e => e.Tipo)
            .ThenBy(e => e.TextoRubro)
            .ThenBy(e => e.Texto)
            .Select(e => new EquivalenciaDto
            {
                Tipo = e.Tipo,
                TextoRubro = e.TextoRubro,
                Texto = e.Texto,
                IdCategoria = e.IdCategoria,
                NombreCategoria = _context.Categorias
                    .Where(c => c.Id == e.IdCategoria)
                    .Select(c => c.NombreCategoria)
                    .FirstOrDefault(),
                IdMarca = e.IdMarca,
                NombreMarca = _context.Marcas
                    .Where(m => m.Id == e.IdMarca)
                    .Select(m => m.NombreMarca)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> BuscarMarcaPorAliasAsync(
        string aliasNormalizado,
        int? idMarcaExcluida,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<AliasMarca>()
            .AsNoTracking()
            .Where(a => a.Alias == aliasNormalizado && a.IdMarca != idMarcaExcluida)
            .Join(_context.Marcas.AsNoTracking(), a => a.IdMarca, m => m.Id, (a, m) => m.NombreMarca)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> BuscarMarcaPorNombreAsync(
        string formaComparacion,
        int? idMarcaExcluida,
        CancellationToken cancellationToken = default)
    {
        // El nombre se guarda con su grafía y Modern_Spanish_CI_AI, que distingue la ñ; la forma de comparación no.
        // El COLLATE de la consulta iguala las dos reglas sin traer los nombres a memoria (Ley 8).
        return await _context.Marcas
            .AsNoTracking()
            .Where(m => EF.Functions.Collate(m.NombreMarca, Cotejamientos.FormaDeComparacion) == formaComparacion)
            .Where(m => m.Id != idMarcaExcluida)
            .Select(m => m.NombreMarca)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> BuscarMarcaPorPrefijoAsync(
        string prefijo,
        int? idMarcaExcluida,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<PrefijoEanMarca>()
            .AsNoTracking()
            .Where(p => p.Prefijo == prefijo && p.IdMarca != idMarcaExcluida)
            .Join(_context.Marcas.AsNoTracking(), p => p.IdMarca, m => m.Id, (p, m) => m.NombreMarca)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> ListarProveedoresConEquivalenciasHaciaAsync(
        TipoEquivalenciaEnum tipo,
        int idDestino,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<EquivalenciaClasificacion>()
            .AsNoTracking()
            .Where(e => e.Tipo == tipo);

        query = tipo == TipoEquivalenciaEnum.Categoria
            ? query.Where(e => e.IdCategoria == idDestino)
            : query.Where(e => e.IdMarca == idDestino);

        // Solo proveedores activos: son los que el repositorio puede cargar para quitar las equivalencias por la raíz.
        return await query
            .Where(e => _context.Proveedores.Any(p => p.Id == e.IdProveedor))
            .Select(e => e.IdProveedor)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
