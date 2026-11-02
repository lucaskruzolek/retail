namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Resumen de una pasada de clasificación del catálogo de un proveedor (Track 6.6, M-75).
/// </summary>
public record class ResultadoClasificacionCatalogoDto
{
    public required int ItemsClasificados { get; init; }

    public required int ConCategoria { get; init; }

    public required int ConMarca { get; init; }

    /// <summary>
    /// Ítems procesados que ninguna regla reconoce: señalan reglas faltantes (M-74).
    /// </summary>
    public required int SinCategoria { get; init; }
}
