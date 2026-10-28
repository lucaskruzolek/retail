using Microsoft.EntityFrameworkCore;
using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Services;

/// <summary>
/// Lectura de los diccionarios del motor de catalogación para un lote (Track 6.6, sección 9). Proyecciones
/// <c>AsNoTracking</c> a DTO; el filtro global de borrado lógico deja solo los datos activos. Términos y prefijos se
/// filtran en SQL Server por los valores del lote: EF Core 8 envía la lista como un único parámetro JSON que el motor
/// desarma con <c>OPENJSON</c> (Ley 8). Las consultas van una después de otra: el contexto no admite operaciones
/// simultáneas (Ley 8 de persistencia).
/// </summary>
public class DiccionariosClasificacionQueryService : IDiccionariosClasificacionQueryService
{
    private readonly RetailDbContext _context;

    public DiccionariosClasificacionQueryService(RetailDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DiccionariosClasificacionDto> ObtenerParaLoteAsync(
        int idProveedor,
        IReadOnlyCollection<string> terminos,
        IReadOnlyCollection<string> prefijos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terminos);
        ArgumentNullException.ThrowIfNull(prefijos);

        var terminosDelLote = await _context.TerminosClasificacion
            .AsNoTracking()
            .Where(t => terminos.Contains(t.Termino))
            .Select(t => new TerminoDiccionarioDto { Termino = t.Termino, IdCategoria = t.IdCategoria })
            .ToListAsync(cancellationToken);

        // Solo los pares con categoría (los ignorados no sugieren, P-4) de los términos ambiguos del lote. Exigir el
        // término ambiguo acá deja afuera un par que haya quedado vigente en un término con categoría por una carrera
        // entre dos usuarios (riesgo aceptado en el paso 5.4).
        var paresDelLote = await _context.Set<ParClasificacion>()
            .AsNoTracking()
            .Where(p => p.IdCategoria != null)
            .Join(
                _context.TerminosClasificacion.Where(t => terminos.Contains(t.Termino) && t.IdCategoria == null),
                p => p.IdTermino,
                t => t.Id,
                (p, t) => new ParDiccionarioDto
                {
                    Termino = t.Termino,
                    SegundaPalabra = p.SegundaPalabra,
                    IdCategoria = p.IdCategoria!.Value
                })
            .ToListAsync(cancellationToken);

        // Completas: buscar una frase dentro de la descripción no se traduce a SQL (son unos cientos de filas). El
        // filtro global también excluye los alias dados de baja de la subconsulta.
        var marcas = await _context.Marcas
            .AsNoTracking()
            .Select(m => new MarcaDiccionarioDto
            {
                IdMarca = m.Id,
                NombreMarca = m.NombreMarca,
                Alias = m.Alias.Select(a => a.Alias).ToList()
            })
            .ToListAsync(cancellationToken);

        // Solo los prefijos de marcas activas: la baja de una marca da de baja sus prefijos, pero la consulta no
        // depende de que eso haya ocurrido.
        var prefijosDelLote = await _context.Set<PrefijoEanMarca>()
            .AsNoTracking()
            .Where(p => prefijos.Contains(p.Prefijo) && _context.Marcas.Any(m => m.Id == p.IdMarca))
            .Select(p => new PrefijoDiccionarioDto { Prefijo = p.Prefijo, IdMarca = p.IdMarca })
            .ToListAsync(cancellationToken);

        var equivalencias = await _context.Set<EquivalenciaClasificacion>()
            .AsNoTracking()
            .Where(e => e.IdProveedor == idProveedor)
            .Select(e => new EquivalenciaDiccionarioDto
            {
                Tipo = e.Tipo,
                TextoRubro = e.TextoRubro,
                Texto = e.Texto,
                IdCategoria = e.IdCategoria,
                IdMarca = e.IdMarca
            })
            .ToListAsync(cancellationToken);

        var categorias = await _context.Categorias
            .AsNoTracking()
            .Select(c => new CategoriaDto { IdCategoria = c.Id, NombreCategoria = c.NombreCategoria, IdRubro = c.IdRubro })
            .ToListAsync(cancellationToken);

        return new DiccionariosClasificacionDto
        {
            Terminos = terminosDelLote,
            Pares = paresDelLote,
            Marcas = marcas,
            Prefijos = prefijosDelLote,
            Equivalencias = equivalencias,
            Categorias = categorias
        };
    }
}
