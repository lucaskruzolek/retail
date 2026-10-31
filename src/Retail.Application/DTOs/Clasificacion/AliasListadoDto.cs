namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Fila de la pestaña de alias (Track 6.6, Fase 6): el alias en forma de comparación y la marca a la que pertenece.
/// </summary>
public record class AliasListadoDto
{
    public required int IdMarca { get; init; }

    public required string NombreMarca { get; init; }

    public required string Alias { get; init; }
}
