namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Sugerencias de clasificación para un ítem del catálogo del proveedor. Se calculan al momento y no se guardan (M-4).
/// </summary>
public record class SugerenciaItemDto
{
    public required int IdCatalogo { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>
    /// Primera palabra de la descripción en forma de comparación: agrupa los ítems de un bloque y es la clave que se
    /// registra en la tabla de términos al decidirlo (M-16).
    /// </summary>
    public required string Termino { get; init; }

    /// <summary>
    /// Nula si ningún nivel sugiere con la precisión exigida, o si el ítem forma parte de un bloque de término nuevo.
    /// </summary>
    public SugerenciaDto? Categoria { get; init; }

    public SugerenciaDto? Marca { get; init; }

    /// <summary>
    /// Verdadero si la categoría se decide en bloque, junto con los demás ítems del mismo término nuevo (M-26).
    /// </summary>
    public bool PerteneceABloque { get; init; }
}
