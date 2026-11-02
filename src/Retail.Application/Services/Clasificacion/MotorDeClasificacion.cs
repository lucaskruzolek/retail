using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Common;
using Retail.Domain.Enums;

namespace Retail.Application.Services.Clasificacion;

/// <summary>
/// Reglas de clasificación por niveles (Track 6.6, sección 4; M-14), sin estado y en memoria. Las comparten
/// <see cref="Services.ClasificacionService"/>, de solo lectura (M-4), y <c>ClasificacionCatalogoService</c>, que guarda
/// las sugerencias del catálogo (M-72, M-77): una sola fuente de reglas para que los dos no diverjan.
/// </summary>
internal static class MotorDeClasificacion
{
    internal static SugerenciaDto? SugerirMarca(
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
    internal static SugerenciaDto? SugerirMarcaDelProveedor(
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

    internal enum EstadoCategoria
    {
        Resuelta,
        TerminoAmbiguo,
        TerminoDesconocido
    }

    internal sealed record CategoriaDelItem(EstadoCategoria Estado, SugerenciaDto? Sugerencia);

    /// <summary>
    /// Niveles de la categoría: la equivalencia del proveedor, la tabla de términos y, para un término ambiguo, su par
    /// con la segunda palabra. Lo que no resuelven queda marcado como término ambiguo (sin sugerencia) o desconocido
    /// (candidato a un bloque).
    /// </summary>
    internal static CategoriaDelItem ClasificarCategoria(
        ItemAClasificar item,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        if (SugerirCategoriaDelProveedor(item, diccionarios, sinTraducir) is { } porEquivalencia)
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
    /// Nivel de la equivalencia de categoría: traduce el valor del proveedor si tiene equivalencia y, si no, lo registra
    /// como "sin traducir". Lo usa también la lista de valores por traducir del catálogo (M-76) sin repetir la regla.
    /// </summary>
    internal static SugerenciaDto? SugerirCategoriaDelProveedor(
        ItemAClasificar item,
        DiccionariosDelLote diccionarios,
        Dictionary<(TipoEquivalenciaEnum, string?, string), int> sinTraducir)
    {
        return ClaveDeCategoriaDelProveedor(item) is var (textoRubro, texto)
            ? SugerirPorEquivalencia(TipoEquivalenciaEnum.Categoria, textoRubro, texto, diccionarios, sinTraducir)
            : null;
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
}
