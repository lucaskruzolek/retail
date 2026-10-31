using Microsoft.EntityFrameworkCore;
using Retail.Domain.Common;
using Retail.Domain.Entities;
using Retail.Infrastructure.Persistence.Context;

namespace Retail.Infrastructure.Persistence.Initialization;

/// <summary>
/// Carga la semilla de diccionarios de clasificación que trae <c>Retail.Cli cargar-diccionarios</c> (Track 6.6, M-60):
/// rubros, categorías, marcas con sus alias y prefijos, términos y pares.
/// </summary>
/// <remarks>
/// <para>
/// Es aditiva e idempotente: crea lo que falta y nunca modifica lo que ya existe, porque puede ser una edición del
/// comercio. Lo que el comercio dio de baja cuenta como existente y no se vuelve a crear (S-2): por eso se lee con
/// <c>IgnoreQueryFilters</c>, ya que los índices únicos filtrados sí permitirían el duplicado. Lo que choca con lo
/// cargado (un alias que ya es de otra marca, un término que el comercio clasificó distinto) se omite y se informa.
/// </para>
/// <para>
/// Los nombres se comparan en la forma de comparación del motor (<see cref="ReglasClasificacion.NormalizarTexto"/>,
/// M-39). Las tablas son chicas (cientos de filas), así que se leen sus nombres con proyecciones y se comparan en
/// memoria, como <see cref="SemilleroDatosDemo"/>.
/// </para>
/// <para>
/// Las fábricas del Dominio reciben Ids (<c>Categoria.Crear(nombre, idRubro)</c>), así que hay un
/// <c>SaveChanges</c> por nivel; la transacción explícita hace que la carga sea todo o nada.
/// </para>
/// </remarks>
public static class SemilleroDiccionarios
{
    public static async Task<ResultadoCargaDiccionarios> CargarAsync(
        RetailDbContext context,
        SemillaDiccionarios semilla,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(semilla);

        var resultado = new ResultadoCargaDiccionarios();
        await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);

        await CargarRubrosAsync(context, semilla, resultado, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await CargarCategoriasAsync(context, semilla, resultado, cancellationToken);
        await CargarMarcasAsync(context, semilla, resultado, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await CargarTerminosYParesAsync(context, semilla, resultado, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await transaccion.CommitAsync(cancellationToken);
        return resultado;
    }

    private static async Task CargarRubrosAsync(
        RetailDbContext context,
        SemillaDiccionarios semilla,
        ResultadoCargaDiccionarios resultado,
        CancellationToken cancellationToken)
    {
        var existentes = await context.Rubros
            .IgnoreQueryFilters()
            .Select(r => new { r.NombreRubro, r.DeletedAt })
            .ToListAsync(cancellationToken);
        var estados = EstadoPorForma(existentes.Select(r => (r.NombreRubro, r.DeletedAt.HasValue)));

        var rubros = semilla.Categorias
            .Select(c => c.Rubro)
            .DistinctBy(ReglasClasificacion.NormalizarTexto);
        foreach (var rubro in rubros)
        {
            switch (estados.GetValueOrDefault(ReglasClasificacion.NormalizarTexto(rubro)))
            {
                case Estado.Activo:
                    resultado.Rubros.SumarExistente();
                    break;
                case Estado.DadoDeBaja:
                    resultado.Rubros.SumarDadoDeBaja();
                    break;
                default:
                    context.Rubros.Add(Rubro.Crear(rubro));
                    resultado.Rubros.SumarCreado();
                    break;
            }
        }
    }

    private static async Task CargarCategoriasAsync(
        RetailDbContext context,
        SemillaDiccionarios semilla,
        ResultadoCargaDiccionarios resultado,
        CancellationToken cancellationToken)
    {
        var rubrosActivos = await context.Rubros
            .Select(r => new { r.Id, r.NombreRubro })
            .ToListAsync(cancellationToken);
        var idRubroPorForma = rubrosActivos.ToDictionary(
            r => ReglasClasificacion.NormalizarTexto(r.NombreRubro),
            r => r.Id);
        var nombreDeRubro = rubrosActivos.ToDictionary(r => r.Id, r => r.NombreRubro);

        var existentes = await context.Categorias
            .IgnoreQueryFilters()
            .Select(c => new { c.NombreCategoria, c.IdRubro, c.DeletedAt })
            .ToListAsync(cancellationToken);
        var estados = EstadoPorForma(existentes.Select(c => (c.NombreCategoria, c.DeletedAt.HasValue)));
        var idRubroDeActiva = existentes
            .Where(c => !c.DeletedAt.HasValue)
            .ToDictionary(c => ReglasClasificacion.NormalizarTexto(c.NombreCategoria), c => c.IdRubro);

        foreach (var fila in semilla.Categorias)
        {
            var forma = ReglasClasificacion.NormalizarTexto(fila.Categoria);
            var idRubro = idRubroPorForma.GetValueOrDefault(ReglasClasificacion.NormalizarTexto(fila.Rubro));

            switch (estados.GetValueOrDefault(forma))
            {
                case Estado.Activo when idRubroDeActiva[forma] != idRubro:
                    // La categoría es única en todo el catálogo (D-8): no se crea otra ni se mueve de rubro (S-3).
                    resultado.ExistenteConDiferencias(
                        resultado.Categorias,
                        $"Categoría '{fila.Categoria}': ya existe en el rubro " +
                        $"'{nombreDeRubro.GetValueOrDefault(idRubroDeActiva[forma])}' y no en '{fila.Rubro}'; " +
                        "se usa la existente.");
                    break;
                case Estado.Activo:
                    resultado.Categorias.SumarExistente();
                    break;
                case Estado.DadoDeBaja:
                    resultado.Categorias.SumarDadoDeBaja();
                    break;
                default:
                    if (idRubro == 0)
                    {
                        resultado.Omitir(
                            resultado.Categorias,
                            $"Categoría '{fila.Categoria}': su rubro '{fila.Rubro}' está dado de baja.");
                        break;
                    }

                    context.Categorias.Add(Categoria.Crear(fila.Categoria, idRubro));
                    resultado.Categorias.SumarCreado();
                    break;
            }
        }
    }

    private static async Task CargarMarcasAsync(
        RetailDbContext context,
        SemillaDiccionarios semilla,
        ResultadoCargaDiccionarios resultado,
        CancellationToken cancellationToken)
    {
        // Las marcas activas se cargan con seguimiento y con sus hijas: se les pueden agregar alias y prefijos.
        var activas = await context.Marcas
            .Include(m => m.Alias)
            .Include(m => m.Prefijos)
            .ToListAsync(cancellationToken);
        var marcaPorForma = activas.ToDictionary(m => ReglasClasificacion.NormalizarTexto(m.NombreMarca));
        var marcasDadasDeBaja = (await context.Marcas
                .IgnoreQueryFilters()
                .Where(m => m.DeletedAt != null)
                .Select(m => m.NombreMarca)
                .ToListAsync(cancellationToken))
            .Select(ReglasClasificacion.NormalizarTexto)
            .ToHashSet();

        var aliasExistentes = await context.Set<AliasMarca>()
            .IgnoreQueryFilters()
            .Select(a => new { a.Alias, a.IdMarca, a.DeletedAt })
            .ToListAsync(cancellationToken);
        var prefijosExistentes = await context.Set<PrefijoEanMarca>()
            .IgnoreQueryFilters()
            .Select(p => new { p.Prefijo, p.IdMarca, p.DeletedAt })
            .ToListAsync(cancellationToken);

        foreach (var fila in semilla.Marcas)
        {
            var forma = ReglasClasificacion.NormalizarTexto(fila.Marca);
            Marca? marca = null;

            if (marcaPorForma.TryGetValue(forma, out var existente))
            {
                marca = existente;
                resultado.Marcas.SumarExistente();
            }
            else if (marcasDadasDeBaja.Contains(forma))
            {
                resultado.Marcas.SumarDadoDeBaja();
            }
            else if (aliasExistentes.Any(a => a.Alias == forma && !a.DeletedAt.HasValue))
            {
                // El nombre de una marca no puede ser alias de otra (M-38).
                resultado.Omitir(resultado.Marcas, $"Marca '{fila.Marca}': ya es alias de otra marca.");
            }
            else
            {
                marca = Marca.Crear(fila.Marca);
                context.Marcas.Add(marca);
                marcaPorForma.Add(forma, marca);
                resultado.Marcas.SumarCreado();
            }

            if (fila.Alias is null)
            {
                continue;
            }

            if (marca is null)
            {
                resultado.Omitir(
                    resultado.Alias,
                    $"Alias '{fila.Alias}': su marca '{fila.Marca}' no se cargó.");
                continue;
            }

            var mismoAlias = aliasExistentes.Where(a => a.Alias == fila.Alias).ToList();
            var aliasActivo = mismoAlias.FirstOrDefault(a => !a.DeletedAt.HasValue);
            if (aliasActivo is not null && aliasActivo.IdMarca == marca.Id)
            {
                resultado.Alias.SumarExistente();
            }
            else if (aliasActivo is not null)
            {
                resultado.Omitir(resultado.Alias, $"Alias '{fila.Alias}': ya es alias de otra marca (M-38).");
            }
            else if (mismoAlias.Count > 0)
            {
                resultado.Alias.SumarDadoDeBaja();
            }
            else if (marcaPorForma.TryGetValue(fila.Alias, out var otra) && otra != marca)
            {
                resultado.Omitir(resultado.Alias, $"Alias '{fila.Alias}': es el nombre de otra marca (M-38).");
            }
            else
            {
                marca.AgregarAlias(fila.Alias);
                resultado.Alias.SumarCreado();
            }
        }

        foreach (var fila in semilla.Prefijos)
        {
            if (!marcaPorForma.TryGetValue(ReglasClasificacion.NormalizarTexto(fila.Marca), out var marca))
            {
                resultado.Omitir(
                    resultado.Prefijos,
                    $"Prefijo '{fila.Prefijo}': su marca '{fila.Marca}' no se cargó.");
                continue;
            }

            var mismoPrefijo = prefijosExistentes.Where(p => p.Prefijo == fila.Prefijo).ToList();
            var prefijoActivo = mismoPrefijo.FirstOrDefault(p => !p.DeletedAt.HasValue);
            if (prefijoActivo is not null && prefijoActivo.IdMarca == marca.Id)
            {
                resultado.Prefijos.SumarExistente();
            }
            else if (prefijoActivo is not null)
            {
                resultado.Omitir(resultado.Prefijos, $"Prefijo '{fila.Prefijo}': ya es de otra marca (M-28).");
            }
            else if (mismoPrefijo.Count > 0)
            {
                resultado.Prefijos.SumarDadoDeBaja();
            }
            else
            {
                marca.AgregarPrefijo(fila.Prefijo);
                resultado.Prefijos.SumarCreado();
            }
        }
    }

    private static async Task CargarTerminosYParesAsync(
        RetailDbContext context,
        SemillaDiccionarios semilla,
        ResultadoCargaDiccionarios resultado,
        CancellationToken cancellationToken)
    {
        var categoriasActivas = await context.Categorias
            .Select(c => new { c.Id, c.NombreCategoria })
            .ToListAsync(cancellationToken);
        var idCategoriaPorForma = categoriasActivas.ToDictionary(
            c => ReglasClasificacion.NormalizarTexto(c.NombreCategoria),
            c => c.Id);

        // Con seguimiento y con sus pares (AutoInclude, M-57): a los ambiguos se les agregan pares.
        var terminoPorForma = await context.TerminosClasificacion
            .ToDictionaryAsync(t => t.Termino, cancellationToken);
        var terminosDadosDeBaja = (await context.TerminosClasificacion
                .IgnoreQueryFilters()
                .Where(t => t.DeletedAt != null)
                .Select(t => t.Termino)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var paresDadosDeBaja = (await context.Set<ParClasificacion>()
                .IgnoreQueryFilters()
                .Where(p => p.DeletedAt != null)
                .Select(p => new { p.TerminoClasificacion!.Termino, p.SegundaPalabra })
                .ToListAsync(cancellationToken))
            .Select(p => (p.Termino, p.SegundaPalabra))
            .ToHashSet();

        foreach (var fila in semilla.Terminos)
        {
            int? idCategoria = fila.Categoria is null
                ? null
                : idCategoriaPorForma.GetValueOrDefault(ReglasClasificacion.NormalizarTexto(fila.Categoria));

            if (terminoPorForma.TryGetValue(fila.Termino, out var existente))
            {
                if (existente.IdCategoria == idCategoria)
                {
                    resultado.Terminos.SumarExistente();
                }
                else
                {
                    resultado.Omitir(
                        resultado.Terminos,
                        $"Término '{fila.Termino}': ya existe con otra clasificación; se deja la del comercio.");
                }
            }
            else if (terminosDadosDeBaja.Contains(fila.Termino))
            {
                resultado.Terminos.SumarDadoDeBaja();
            }
            else if (idCategoria == 0)
            {
                resultado.Omitir(
                    resultado.Terminos,
                    $"Término '{fila.Termino}': su categoría '{fila.Categoria}' no está disponible.");
            }
            else
            {
                var termino = idCategoria is null
                    ? TerminoClasificacion.CrearAmbiguo(fila.Termino)
                    : TerminoClasificacion.CrearConCategoria(fila.Termino, idCategoria.Value);
                context.TerminosClasificacion.Add(termino);
                terminoPorForma.Add(fila.Termino, termino);
                resultado.Terminos.SumarCreado();
            }
        }

        foreach (var fila in semilla.Pares)
        {
            var descripcion = $"Par '{fila.Termino} {fila.SegundaPalabra}'";
            if (!terminoPorForma.TryGetValue(fila.Termino, out var termino))
            {
                resultado.Omitir(resultado.Pares, $"{descripcion}: su término no se cargó.");
                continue;
            }

            if (!termino.EsAmbiguo)
            {
                // Un par solo existe mientras su término es ambiguo (M-52).
                resultado.Omitir(resultado.Pares, $"{descripcion}: el comercio le asignó una categoría al término.");
                continue;
            }

            var idCategoria = idCategoriaPorForma.GetValueOrDefault(ReglasClasificacion.NormalizarTexto(fila.Categoria));
            var vigente = termino.Pares.FirstOrDefault(p => !p.IsDeleted && p.SegundaPalabra == fila.SegundaPalabra);
            if (vigente is not null && vigente.IdCategoria == idCategoria)
            {
                resultado.Pares.SumarExistente();
            }
            else if (vigente is not null)
            {
                resultado.Omitir(
                    resultado.Pares,
                    $"{descripcion}: ya existe con otro destino (o ignorado); se deja el del comercio.");
            }
            else if (paresDadosDeBaja.Contains((fila.Termino, fila.SegundaPalabra)))
            {
                resultado.Pares.SumarDadoDeBaja();
            }
            else if (idCategoria == 0)
            {
                resultado.Omitir(
                    resultado.Pares,
                    $"{descripcion}: su categoría '{fila.Categoria}' no está disponible.");
            }
            else
            {
                termino.AsignarPar(fila.SegundaPalabra, idCategoria);
                resultado.Pares.SumarCreado();
            }
        }
    }

    /// <summary>
    /// Estado de cada nombre en la base, en su forma de comparación. Si hay uno activo y otros dados de baja con el
    /// mismo nombre (se borró y se volvió a crear), manda el activo.
    /// </summary>
    private static Dictionary<string, Estado> EstadoPorForma(IEnumerable<(string Nombre, bool DadoDeBaja)> filas)
    {
        return filas
            .GroupBy(f => ReglasClasificacion.NormalizarTexto(f.Nombre))
            .ToDictionary(g => g.Key, g => g.Any(f => !f.DadoDeBaja) ? Estado.Activo : Estado.DadoDeBaja);
    }

    private enum Estado
    {
        NoExiste = 0,
        Activo,
        DadoDeBaja
    }
}
