namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Una categoría o una marca sugerida, con su motivo y, si la sugirió el modelo, su confianza (RF-27). Nunca se aplica
/// sin que una persona la confirme.
/// </summary>
public record class SugerenciaDto
{
    /// <summary>
    /// Identificador de la categoría o de la marca sugerida.
    /// </summary>
    public required int Id { get; init; }

    public required string Nombre { get; init; }

    public required MotivoSugerenciaEnum Motivo { get; init; }

    /// <summary>
    /// Probabilidad del modelo, entre 0 y 1; nula en las sugerencias deterministas (equivalencias y diccionarios).
    /// </summary>
    public double? Confianza { get; init; }
}
