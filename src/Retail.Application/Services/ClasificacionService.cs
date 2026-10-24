using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Infrastructure;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services.Clasificacion;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Sugerencia de categoría y marca por niveles (Track 6.6, sección 4; RF-27). Cada nivel resuelve lo que puede y deja
/// pasar el resto, de lo más confiable a lo más incierto (M-14):
/// <list type="number">
/// <item>Lo que informó el proveedor: su marca coincide con una del comercio, o una equivalencia traduce su valor.</item>
/// <item>Los diccionarios: término no ambiguo; marca nombrada en la descripción, con el prefijo EAN solo para desempatar
/// o cuando el texto no nombra ninguna (M-25).</item>
/// <item>Términos nuevos frecuentes: se deciden en bloque, antes que el modelo (M-26).</item>
/// <item>El modelo, solo con confianza mayor o igual al umbral τ (sección 5.3).</item>
/// </list>
/// Todo ocurre en memoria después de dos lecturas (el lote y sus diccionarios); no modifica nada (M-4).
/// </summary>
public class ClasificacionService : IClasificacionService
{
    /// <summary>
    /// Cantidad mínima de ítems con el mismo término nuevo para decidirlos en bloque (M-26).
    /// </summary>
    public const int TamanoMinimoDeBloque = 5;

    private const int EjemplosPorBloque = 3;

    private readonly ICatalogoProveedorQueryService _catalogo;
    private readonly IDiccionariosClasificacionQueryService _diccionarios;
    private readonly IModeloCategoria _modelo;
    private readonly IValidator<SugerirClasificacionDto> _validator;

    public ClasificacionService(
        ICatalogoProveedorQueryService catalogo,
        IDiccionariosClasificacionQueryService diccionarios,
        IModeloCategoria modelo,
        IValidator<SugerirClasificacionDto> validator)
    {
        _catalogo = catalogo;
        _diccionarios = diccionarios;
        _modelo = modelo;
        _validator = validator;
    }

    public async Task<SugerenciasClasificacionDto> SugerirAsync(SugerirClasificacionDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _validator.ValidateAndThrowAsync(dto, cancellationToken);

        var items = await CargarItemsAsync(dto, cancellationToken);
        var diccionarios = new DiccionariosDelLote(await _diccionarios.ObtenerParaLoteAsync(
            dto.IdProveedor,
            items.Select(i => i.Termino).Where(t => t.Length > 0).Distinct().ToList(),
            items.Select(i => i.PrefijoTitular).OfType<string>().Distinct().ToList(),
            cancellationToken));

        var sinTraducir = new Dictionary<(TipoEquivalenciaEnum Tipo, string? Rubro, string Texto), int>();
        var marcas = items.ToDictionary(i => i.IdCatalogo, i => SugerirMarca(i, diccionarios, sinTraducir));
        var categorias = items.ToDictionary(i => i.IdCatalogo, i => SugerirCategoriaSinModelo(i, diccionarios, sinTraducir));

        // Términos nuevos frecuentes: se deciden en bloque antes que el modelo (M-26). Un ítem sin descripción no
        // tiene término que registrar.
        var terminosEnBloque = items
            .Where(i => categorias[i.IdCatalogo].Estado == EstadoCategoria.TerminoDesconocido && i.Termino.Length > 0)
            .GroupBy(i => i.Termino)
            .Where(g => g.Count() >= TamanoMinimoDeBloque)
            .ToDictionary(g => g.Key, g => g.ToList());

        var predicciones = Predecir(items.Where(i => categorias[i.IdCatalogo].Estado != EstadoCategoria.Resuelta).ToList());

        return new SugerenciasClasificacionDto
        {
            Items = items.Select(i => new SugerenciaItemDto
            {
                IdCatalogo = i.IdCatalogo,
                Descripcion = i.Descripcion,
                Termino = i.Termino,
                Categoria = terminosEnBloque.ContainsKey(i.Termino)
                    ? null
                    : categorias[i.IdCatalogo].Sugerencia ?? SugerirPorModelo(predicciones.GetValueOrDefault(i.IdCatalogo), diccionarios),
                Marca = marcas[i.IdCatalogo],
                PerteneceABloque = terminosEnBloque.ContainsKey(i.Termino)
            }).ToList(),
            Bloques = terminosEnBloque
                .Select(g => ArmarBloque(g.Key, g.Value, predicciones, diccionarios))
                .OrderByDescending(b => b.IdsCatalogo.Count)
                .ThenBy(b => b.Termino, StringComparer.Ordinal)
                .ToList(),
            ValoresSinTraducir = sinTraducir
                .Select(v => new ValorSinTraducirDto { Tipo = v.Key.Tipo, TextoRubro = v.Key.Rubro, Texto = v.Key.Texto, CantidadDeItems = v.Value })
                .OrderByDescending(v => v.CantidadDeItems)
                .ThenBy(v => v.Texto, StringComparer.Ordinal)
                .ToList(),
            ModeloDisponible = _modelo.EstaDisponible
        };
    }

    private async Task<List<ItemAClasificar>> CargarItemsAsync(SugerirClasificacionDto dto, CancellationToken cancellationToken)
    {
        var ids = dto.IdsCatalogo.Distinct().ToList();
        var catalogos = (await _catalogo.ObtenerPorIdsAsync(ids, cancellationToken)).ToDictionary(c => c.Id);

        var inexistentes = ids.Where(id => !catalogos.ContainsKey(id)).ToList();
        if (inexistentes.Count > 0)
        {
            throw new DomainException($"No se encontraron los ítems de catálogo {string.Join(", ", inexistentes)}.");
        }

        if (catalogos.Values.Any(c => c.IdProveedor != dto.IdProveedor))
        {
            throw new DomainException("Todos los ítems a clasificar deben pertenecer al mismo proveedor.");
        }

        // Se respeta el orden pedido: es el de la grilla de la pantalla de incorporación.
        return ids.Select(id => ItemAClasificar.Desde(catalogos[id])).ToList();
    }

    // ---------- Marca ----------

    private static SugerenciaDto? SugerirMarca(
        ItemAClasificar item,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        return SugerirMarcaDelProveedor(item, diccionarios, sinTraducir) ?? SugerirMarcaPorDescripcion(item, diccionarios);
    }

    /// <summary>
    /// La marca de la columna de la planilla se busca primero entre los nombres y alias del comercio; si no aparece,
    /// en las equivalencias del proveedor (sección 4.1).
    /// </summary>
    private static SugerenciaDto? SugerirMarcaDelProveedor(
        ItemAClasificar item,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        if (item.MarcaProveedor is null)
        {
            return null;
        }

        var texto = ReglasClasificacion.NormalizarTexto(item.MarcaProveedor);
        if (diccionarios.Marcas.BuscarExacta(texto) is int idMarca)
        {
            return diccionarios.SugerirMarca(idMarca, MotivoSugerenciaEnum.MarcaInformadaPorElProveedor);
        }

        return SugerirPorEquivalencia(TipoEquivalenciaEnum.Marca, null, texto, diccionarios, sinTraducir);
    }

    /// <summary>
    /// Texto primero; el prefijo del titular decide si el texto no nombra ninguna marca y desempata si nombra varias
    /// (M-25). Si nombra varias y el prefijo no es de ninguna, no sugiere.
    /// </summary>
    private static SugerenciaDto? SugerirMarcaPorDescripcion(ItemAClasificar item, DiccionariosDelLote diccionarios)
    {
        var enLaDescripcion = diccionarios.Marcas.MarcasEn(item.PalabrasParaMarca);
        var delPrefijo = diccionarios.MarcaDelPrefijo(item.PrefijoTitular);

        return enLaDescripcion.Count switch
        {
            1 => diccionarios.SugerirMarca(enLaDescripcion.Single(), MotivoSugerenciaEnum.MarcaEnLaDescripcion),
            > 1 when delPrefijo is int desempate && enLaDescripcion.Contains(desempate) =>
                diccionarios.SugerirMarca(desempate, MotivoSugerenciaEnum.MarcaEnLaDescripcion),
            > 1 => null,
            _ when delPrefijo is int idMarca => diccionarios.SugerirMarca(idMarca, MotivoSugerenciaEnum.PrefijoDelCodigoDeBarras),
            _ => null
        };
    }

    // ---------- Categoría ----------

    private enum EstadoCategoria
    {
        Resuelta,
        TerminoAmbiguo,
        TerminoDesconocido
    }

    private sealed record CategoriaSinModelo(EstadoCategoria Estado, SugerenciaDto? Sugerencia);

    /// <summary>
    /// Niveles deterministas de la categoría: la equivalencia del proveedor y la tabla de términos. Lo que no resuelven
    /// queda marcado como término ambiguo o desconocido, para el bloque o el modelo.
    /// </summary>
    private static CategoriaSinModelo SugerirCategoriaSinModelo(
        ItemAClasificar item,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        if (ClaveDeCategoriaDelProveedor(item) is var (textoRubro, texto)
            && SugerirPorEquivalencia(TipoEquivalenciaEnum.Categoria, textoRubro, texto, diccionarios, sinTraducir) is { } porEquivalencia)
        {
            return new CategoriaSinModelo(EstadoCategoria.Resuelta, porEquivalencia);
        }

        var termino = diccionarios.BuscarTermino(item.Termino);
        if (termino is null)
        {
            return new CategoriaSinModelo(EstadoCategoria.TerminoDesconocido, null);
        }

        // Un término cuya categoría ya no está activa no sugiere: se comporta como ambiguo y decide el modelo.
        var porTermino = termino.IdCategoria is int idCategoria
            ? diccionarios.SugerirCategoria(idCategoria, MotivoSugerenciaEnum.Termino)
            : null;
        return porTermino is null
            ? new CategoriaSinModelo(EstadoCategoria.TerminoAmbiguo, null)
            : new CategoriaSinModelo(EstadoCategoria.Resuelta, porTermino);
    }

    /// <summary>
    /// Clave de la equivalencia de categoría (M-31): la categoría del proveedor con su rubro como contexto, o el rubro
    /// solo si es lo único que trae la planilla.
    /// </summary>
    private static (string? TextoRubro, string Texto)? ClaveDeCategoriaDelProveedor(ItemAClasificar item)
    {
        if (item.CategoriaProveedor is not null)
        {
            var rubro = item.RubroProveedor is null ? null : ReglasClasificacion.NormalizarTexto(item.RubroProveedor);
            return (rubro, ReglasClasificacion.NormalizarTexto(item.CategoriaProveedor));
        }

        return item.RubroProveedor is null ? null : (null, ReglasClasificacion.NormalizarTexto(item.RubroProveedor));
    }

    /// <summary>
    /// Traduce un valor del proveedor. Si no tiene equivalencia, lo registra como "sin traducir" (una vez por valor,
    /// contando los ítems); si está ignorada o su destino ya no está activo, deja seguir a los otros niveles.
    /// </summary>
    private static SugerenciaDto? SugerirPorEquivalencia(
        TipoEquivalenciaEnum tipo,
        string? textoRubro,
        string texto,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        var equivalencia = diccionarios.BuscarEquivalencia(tipo, textoRubro, texto);
        if (equivalencia is null)
        {
            var clave = (tipo, textoRubro, texto);
            sinTraducir[clave] = sinTraducir.GetValueOrDefault(clave) + 1;
            return null;
        }

        return equivalencia switch
        {
            { IdCategoria: int idCategoria } => diccionarios.SugerirCategoria(idCategoria, MotivoSugerenciaEnum.EquivalenciaDelProveedor),
            { IdMarca: int idMarca } => diccionarios.SugerirMarca(idMarca, MotivoSugerenciaEnum.EquivalenciaDelProveedor),
            _ => null
        };
    }

    // ---------- Modelo y bloques ----------

    /// <summary>
    /// Una sola llamada al modelo para todos los ítems que no resolvieron los diccionarios (también los de bloque, para
    /// su propuesta). Sin modelo, no hay predicciones (arranque en frío, M-37).
    /// </summary>
    private Dictionary<int, PrediccionCategoriaDto> Predecir(List<ItemAClasificar> pendientes)
    {
        if (!_modelo.EstaDisponible || pendientes.Count == 0)
        {
            return new Dictionary<int, PrediccionCategoriaDto>();
        }

        var predicciones = _modelo.Predecir(pendientes.Select(i => i.Descripcion).ToList());
        return pendientes.Zip(predicciones).ToDictionary(p => p.First.IdCatalogo, p => p.Second);
    }

    private static SugerenciaDto? SugerirPorModelo(PrediccionCategoriaDto? prediccion, DiccionariosDelLote diccionarios)
    {
        return prediccion is { SuperaUmbral: true }
            ? diccionarios.SugerirCategoria(prediccion.IdCategoria, MotivoSugerenciaEnum.Modelo, prediccion.Confianza)
            : null;
    }

    /// <summary>
    /// La propuesta del bloque es la categoría activa que el modelo asigna a la mayoría del grupo, aunque no alcance el
    /// umbral por producto (M-26). Empate: la de mayor confianza promedio y, después, el menor Id (determinismo).
    /// </summary>
    private static BloqueTerminoNuevoDto ArmarBloque(
        string termino,
        IReadOnlyList<ItemAClasificar> items,
        Dictionary<int, PrediccionCategoriaDto> predicciones,
        DiccionariosDelLote diccionarios)
    {
        var mayoria = items
            .Select(i => predicciones.GetValueOrDefault(i.IdCatalogo))
            .OfType<PrediccionCategoriaDto>()
            .Where(p => diccionarios.EsCategoriaActiva(p.IdCategoria))
            .GroupBy(p => p.IdCategoria)
            .Select(g => new { IdCategoria = g.Key, Cantidad = g.Count(), Confianza = g.Average(p => p.Confianza) })
            .OrderByDescending(g => g.Cantidad)
            .ThenByDescending(g => g.Confianza)
            .ThenBy(g => g.IdCategoria)
            .FirstOrDefault();

        return new BloqueTerminoNuevoDto
        {
            Termino = termino,
            IdsCatalogo = items.Select(i => i.IdCatalogo).ToList(),
            Propuesta = mayoria is null
                ? null
                : diccionarios.SugerirCategoria(mayoria.IdCategoria, MotivoSugerenciaEnum.Modelo, mayoria.Confianza),
            CoincidenConLaPropuesta = mayoria?.Cantidad ?? 0,
            Ejemplos = items.Take(EjemplosPorBloque).Select(i => i.Descripcion).ToList()
        };
    }
}
