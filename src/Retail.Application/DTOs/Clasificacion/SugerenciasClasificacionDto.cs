namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Resultado de clasificar un lote del catálogo de un proveedor (RF-27): sugerencias por ítem, bloques de términos
/// nuevos (M-26) y valores del proveedor sin traducir (M-13).
/// </summary>
public record class SugerenciasClasificacionDto
{
    public required IReadOnlyList<SugerenciaItemDto> Items { get; init; }

    public required IReadOnlyList<BloqueTerminoNuevoDto> Bloques { get; init; }

    public required IReadOnlyList<ValorSinTraducirDto> ValoresSinTraducir { get; init; }

    /// <summary>
    /// Falso en el arranque en frío (sección 7, M-37): la pantalla puede explicar por qué hay menos sugerencias y por
    /// qué los bloques llegan sin propuesta.
    /// </summary>
    public required bool ModeloDisponible { get; init; }
}
