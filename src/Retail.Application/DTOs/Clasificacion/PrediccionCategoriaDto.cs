namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Respuesta del modelo de categorías para una descripción (Track 6.6, sección 5): la categoría más probable, aunque
/// no alcance el umbral, porque la decisión en bloque la usa igual como propuesta (M-26).
/// </summary>
public record class PrediccionCategoriaDto
{
    public required int IdCategoria { get; init; }

    /// <summary>
    /// Probabilidad de la categoría predicha, entre 0 y 1.
    /// </summary>
    public required double Confianza { get; init; }

    /// <summary>
    /// Si la confianza alcanza el umbral τ calibrado en el último entrenamiento (M-10). Solo entonces el modelo
    /// sugiere por producto.
    /// </summary>
    public required bool SuperaUmbral { get; init; }
}
