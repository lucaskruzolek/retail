using FluentValidation;
using Retail.Application.DTOs.Clasificacion;
using Retail.Application.Interfaces.Persistence;
using Retail.Application.Interfaces.Services;
using Retail.Application.Services.Clasificacion;
using Retail.Domain.Common;
using Retail.Domain.Enums;
using Retail.Domain.Exceptions;

namespace Retail.Application.Services;

/// <summary>
/// Sugerencia de categoría y marca por niveles (Track 6.6, sección 4; RF-27). Cada nivel resuelve lo que puede y deja
/// pasar el resto, de lo más confiable a lo menos (M-14). Todos son reglas aprobadas por una persona (M-45, M-49):
/// <list type="number">
/// <item>Lo que informó el proveedor: su marca coincide con una del comercio, o una equivalencia traduce su valor.</item>
/// <item>Los diccionarios: término no ambiguo, o par "término ambiguo + segunda palabra" (Fase 5, M-48); marca nombrada
/// en la descripción, con el prefijo EAN solo para desempatar (M-25, M-69).</item>
/// <item>Términos nuevos frecuentes: se agrupan para decidirlos en bloque (M-26).</item>
/// </list>
/// Lo que ninguna regla resuelve queda sin sugerencia.
/// Todo ocurre en memoria después de dos lecturas (el lote y sus diccionarios); no modifica nada (M-4).
/// </summary>
public class ClasificacionService : IClasificacionService
{
    /// <summary>
    /// Cantidad mínima de ítems con el mismo término nuevo para decidirlos en bloque (M-26).
    /// </summary>
    public const int TamanoMinimoDeBloque = 3;

    private const int EjemplosPorBloque = 3;

    private readonly ICatalogoProveedorQueryService _catalogo;
    private readonly IDiccionariosClasificacionQueryService _diccionarios;
    private readonly IValidator<SugerirClasificacionDto> _validator;

    public ClasificacionService(
        ICatalogoProveedorQueryService catalogo,
        IDiccionariosClasificacionQueryService diccionarios,
        IValidator<SugerirClasificacionDto> validator)
    {
        _catalogo = catalogo;
        _diccionarios = diccionarios;
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
        var categorias = items.ToDictionary(i => i.IdCatalogo, i => ClasificarCategoria(i, diccionarios, sinTraducir));

        // Términos nuevos frecuentes: se deciden en bloque (M-26). Un ítem sin descripción no tiene término que
        // registrar.
        var terminosEnBloque = items
            .Where(i => categorias[i.IdCatalogo].Estado == EstadoCategoria.TerminoDesconocido && i.Termino.Length > 0)
            .GroupBy(i => i.Termino)
            .Where(g => g.Count() >= TamanoMinimoDeBloque)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new SugerenciasClasificacionDto
        {
            Items = items.Select(i => new SugerenciaItemDto
            {
                IdCatalogo = i.IdCatalogo,
                Descripcion = i.Descripcion,
                Termino = i.Termino,
                Categoria = terminosEnBloque.ContainsKey(i.Termino) ? null : categorias[i.IdCatalogo].Sugerencia,
                Marca = marcas[i.IdCatalogo],
                PerteneceABloque = terminosEnBloque.ContainsKey(i.Termino)
            }).ToList(),
            Bloques = terminosEnBloque
                .Select(g => ArmarBloque(g.Key, g.Value))
                .OrderByDescending(b => b.IdsCatalogo.Count)
                .ThenBy(b => b.Termino, StringComparer.Ordinal)
                .ToList(),
            ValoresSinTraducir = sinTraducir
                .Select(v => new ValorSinTraducirDto { Tipo = v.Key.Tipo, TextoRubro = v.Key.Rubro, Texto = v.Key.Texto, CantidadDeItems = v.Value })
                .OrderByDescending(v => v.CantidadDeItems)
                .ThenBy(v => v.Texto, StringComparer.Ordinal)
                .ToList()
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
    /// Solo el texto sugiere la marca; el prefijo del titular desempata si el texto nombra varias (M-25). Si el texto no
    /// nombra ninguna, el prefijo no decide (M-69): identifica a la empresa titular, no a la marca. Si nombra varias y el
    /// prefijo no es de ninguna, no sugiere.
    /// </summary>
    private static SugerenciaDto? SugerirMarcaPorDescripcion(ItemAClasificar item, DiccionariosDelLote diccionarios)
    {
        var enLaDescripcion = diccionarios.Marcas.MarcasEn(item.PalabrasParaMarca);

        return enLaDescripcion.Count switch
        {
            1 => diccionarios.SugerirMarca(enLaDescripcion.Single(), MotivoSugerenciaEnum.MarcaEnLaDescripcion),
            > 1 when diccionarios.MarcaDelPrefijo(item.PrefijoTitular) is int desempate && enLaDescripcion.Contains(desempate) =>
                diccionarios.SugerirMarca(desempate, MotivoSugerenciaEnum.MarcaEnLaDescripcion),
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

    private sealed record CategoriaDelItem(EstadoCategoria Estado, SugerenciaDto? Sugerencia);

    /// <summary>
    /// Niveles de la categoría: la equivalencia del proveedor, la tabla de términos y, para un término ambiguo, su par
    /// con la segunda palabra. Lo que no resuelven queda marcado como término ambiguo (sin sugerencia) o desconocido
    /// (candidato a un bloque).
    /// </summary>
    private static CategoriaDelItem ClasificarCategoria(
        ItemAClasificar item,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        if (ClaveDeCategoriaDelProveedor(item) is var (textoRubro, texto)
            && SugerirPorEquivalencia(TipoEquivalenciaEnum.Categoria, textoRubro, texto, diccionarios, sinTraducir) is { } porEquivalencia)
        {
            return new CategoriaDelItem(EstadoCategoria.Resuelta, porEquivalencia);
        }

        var termino = diccionarios.BuscarTermino(item.Termino);
        if (termino is null)
        {
            return new CategoriaDelItem(EstadoCategoria.TerminoDesconocido, null);
        }

        // Un término cuya categoría ya no está activa no sugiere: se comporta como ambiguo. No tiene pares (P-3), así
        // que no pasa por SugerirPorPar.
        var sugerencia = termino.IdCategoria is int idCategoria
            ? diccionarios.SugerirCategoria(idCategoria, MotivoSugerenciaEnum.Termino)
            : SugerirPorPar(item, diccionarios);
        return sugerencia is null
            ? new CategoriaDelItem(EstadoCategoria.TerminoAmbiguo, null)
            : new CategoriaDelItem(EstadoCategoria.Resuelta, sugerencia);
    }

    /// <summary>
    /// Par "término ambiguo + segunda palabra" del diccionario (Fase 5, M-48). Sin segunda palabra, sin par con
    /// categoría o con la categoría dada de baja, no sugiere.
    /// </summary>
    private static SugerenciaDto? SugerirPorPar(ItemAClasificar item, DiccionariosDelLote diccionarios)
    {
        if (item.SegundaPalabra.Length == 0)
        {
            return null;
        }

        return diccionarios.CategoriaDelPar(item.Termino, item.SegundaPalabra) is int idCategoria
            ? diccionarios.SugerirCategoria(idCategoria, MotivoSugerenciaEnum.ParDelDiccionario)
            : null;
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

    // ---------- Bloques ----------

    /// <summary>
    /// Un bloque llega sin categoría propuesta: la decide el usuario una sola vez para todo el grupo y el término queda
    /// registrado (M-16, M-26). Los ejemplos hacen informada la decisión.
    /// </summary>
    private static BloqueTerminoNuevoDto ArmarBloque(string termino, IReadOnlyList<ItemAClasificar> items)
    {
        return new BloqueTerminoNuevoDto
        {
            Termino = termino,
            IdsCatalogo = items.Select(i => i.IdCatalogo).ToList(),
            Ejemplos = items.Take(EjemplosPorBloque).Select(i => i.Descripcion).ToList()
        };
    }
}
