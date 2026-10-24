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
    private readonly Dictionary<string, int> _marcaPorPrefijo;
    private readonly Dictionary<(TipoEquivalenciaEnum, string?, string), EquivalenciaDiccionarioDto> _equivalencias;

    public DiccionariosDelLote(DiccionariosClasificacionDto diccionarios)
    {
        _categorias = diccionarios.Categorias.ToDictionary(c => c.IdCategoria);
        _nombresDeMarca = diccionarios.Marcas.ToDictionary(m => m.IdMarca, m => m.NombreMarca);
        _terminos = diccionarios.Terminos.ToDictionary(t => t.Termino, StringComparer.Ordinal);
        _marcaPorPrefijo = diccionarios.Prefijos.ToDictionary(p => p.Prefijo, p => p.IdMarca, StringComparer.Ordinal);
        _equivalencias = diccionarios.Equivalencias.ToDictionary(e => (e.Tipo, e.TextoRubro, e.Texto));
        Marcas = new BuscadorDeMarcas(diccionarios.Marcas);
    }

    public BuscadorDeMarcas Marcas { get; }

    public TerminoDiccionarioDto? BuscarTermino(string termino)
    {
        return _terminos.GetValueOrDefault(termino);
    }

    public int? MarcaDelPrefijo(string? prefijo)
    {
        return prefijo is not null && _marcaPorPrefijo.TryGetValue(prefijo, out var idMarca) ? idMarca : null;
    }

    public EquivalenciaDiccionarioDto? BuscarEquivalencia(TipoEquivalenciaEnum tipo, string? textoRubro, string texto)
    {
        return _equivalencias.GetValueOrDefault((tipo, textoRubro, texto));
    }

    public bool EsCategoriaActiva(int idCategoria)
    {
        return _categorias.ContainsKey(idCategoria);
    }

    /// <summary>
    /// Sugerencia de categoría, o nula si la categoría no está activa (dada de baja después del entrenamiento o de
    /// registrar la regla).
    /// </summary>
    public SugerenciaDto? SugerirCategoria(int idCategoria, MotivoSugerenciaEnum motivo, double? confianza = null)
    {
        return _categorias.TryGetValue(idCategoria, out var categoria)
            ? new SugerenciaDto { Id = idCategoria, Nombre = categoria.NombreCategoria, Motivo = motivo, Confianza = confianza }
            : null;
    }

    public SugerenciaDto? SugerirMarca(int idMarca, MotivoSugerenciaEnum motivo)
    {
        return _nombresDeMarca.TryGetValue(idMarca, out var nombre)
            ? new SugerenciaDto { Id = idMarca, Nombre = nombre, Motivo = motivo }
            : null;
    }
}
