namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Fila de la pestaña de prefijos (Track 6.6, Fase 6): el prefijo del titular y la marca a la que pertenece.
/// </summary>
public record class PrefijoListadoDto
{
    public required int IdMarca { get; init; }

    public required string NombreMarca { get; init; }

    public required string Prefijo { get; init; }
}
