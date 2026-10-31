using Retail.Application.DTOs.Articulos;

namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Los diccionarios que necesita la clasificación de un lote del catálogo de un proveedor (Track 6.6, sección 4),
/// leídos una vez por lote. Contiene solo datos activos.
/// </summary>
public record class DiccionariosClasificacionDto
{
    /// <summary>
    /// Términos de la tabla que coinciden con las primeras palabras del lote (no toda la tabla).
    /// </summary>
    public required IReadOnlyList<TerminoDiccionarioDto> Terminos { get; init; }

    /// <summary>
    /// Pares con categoría de los términos ambiguos del lote (Fase 5). Un par de un término con categoría no llega:
    /// por P-3 no debería existir, y el motor solo consulta pares de términos ambiguos.
    /// </summary>
    public required IReadOnlyList<ParDiccionarioDto> Pares { get; init; }

    /// <summary>
    /// Todas las marcas activas con sus alias: buscar una frase dentro de la descripción no se traduce a SQL.
    /// </summary>
    public required IReadOnlyList<MarcaDiccionarioDto> Marcas { get; init; }

    /// <summary>
    /// Prefijos registrados que coinciden con los prefijos del lote.
    /// </summary>
    public required IReadOnlyList<PrefijoDiccionarioDto> Prefijos { get; init; }

    /// <summary>
    /// Equivalencias del proveedor del lote (M-13), incluidas las ignoradas.
    /// </summary>
    public required IReadOnlyList<EquivalenciaDiccionarioDto> Equivalencias { get; init; }

    /// <summary>
    /// Categorías activas con su rubro: dan el nombre de cada sugerencia y descartan las reglas que apuntan a una
    /// categoría dada de baja.
    /// </summary>
    public required IReadOnlyList<CategoriaDto> Categorias { get; init; }
}
