using Retail.Application.DTOs.Articulos;
using Retail.Application.DTOs.Clasificacion;
using Retail.Domain.Enums;

namespace Retail.Application.Services.Clasificacion;

/// <summary>
/// Los diccionarios de un lote convertidos en búsquedas por clave, para clasificar miles de ítems sin volver a la base
/// (Track 6.6, sección 4).
/// </summary>
internal sealed class DiccionariosDelLote
{
    private readonly Dictionary<int, CategoriaDto> _categorias;
    private readonly Dictionary<int, string> _nombresDeMarca;
    private readonly Dictionary<string, TerminoDiccionarioDto> _terminos;
    private readonly Dictionary<(string Termino, string SegundaPalabra), int> _categoriaPorPar;
    private readonly Dictionary<string, int> _marcaPorPrefijo;
    private readonly Dictionary<(TipoEquivalenciaEnum, string?, string), EquivalenciaDiccionarioDto> _equivalencias;

    public DiccionariosDelLote(DiccionariosClasificacionDto diccionarios)
    {
        _categorias = diccionarios.Categorias.ToDictionary(c => c.IdCategoria);
        _nombresDeMarca = diccionarios.Marcas.ToDictionary(m => m.IdMarca, m => m.NombreMarca);
        _terminos = diccionarios.Terminos.ToDictionary(t => t.Termino, StringComparer.Ordinal);
        _categoriaPorPar = diccionarios.Pares.ToDictionary(p => (p.Termino, p.SegundaPalabra), p => p.IdCategoria);
        _marcaPorPrefijo = diccionarios.Prefijos.ToDictionary(p => p.Prefijo, p => p.IdMarca, StringComparer.Ordinal);
        _equivalencias = diccionarios.Equivalencias.ToDictionary(e => (e.Tipo, e.TextoRubro, e.Texto));
        Marcas = new BuscadorDeMarcas(diccionarios.Marcas);
    }

    public BuscadorDeMarcas Marcas { get; }

    public TerminoDiccionarioDto? BuscarTermino(string termino)
    {
        return _terminos.GetValueOrDefault(termino);
    }

    /// <summary>
    /// Categoría del par "término + segunda palabra", o nula si el diccionario no tiene el par con categoría.
    /// </summary>
    public int? CategoriaDelPar(string termino, string segundaPalabra)
    {
        return _categoriaPorPar.TryGetValue((termino, segundaPalabra), out var idCategoria) ? idCategoria : null;
    }

    public int? MarcaDelPrefijo(string? prefijo)
    {
        return prefijo is not null && _marcaPorPrefijo.TryGetValue(prefijo, out var idMarca) ? idMarca : null;
    }

    public EquivalenciaDiccionarioDto? BuscarEquivalencia(TipoEquivalenciaEnum tipo, string? textoRubro, string texto)
    {
        return _equivalencias.GetValueOrDefault((tipo, textoRubro, texto));
    }

    /// <summary>
    /// Sugerencia de categoría, o nula si la categoría no está activa (dada de baja después de registrar la regla).
    /// </summary>
    public SugerenciaDto? SugerirCategoria(int idCategoria, MotivoSugerenciaEnum motivo)
    {
        return _categorias.TryGetValue(idCategoria, out var categoria)
            ? new SugerenciaDto { Id = idCategoria, Nombre = categoria.NombreCategoria, Motivo = motivo }
            : null;
    }

    public SugerenciaDto? SugerirMarca(int idMarca, MotivoSugerenciaEnum motivo)
    {
        return _nombresDeMarca.TryGetValue(idMarca, out var nombre)
            ? new SugerenciaDto { Id = idMarca, Nombre = nombre, Motivo = motivo }
            : null;
    }
}
