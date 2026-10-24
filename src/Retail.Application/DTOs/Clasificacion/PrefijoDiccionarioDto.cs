namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Prefijo del titular de un código de barras y la marca a la que pertenece (M-25, M-28).
/// </summary>
public record class PrefijoDiccionarioDto
{
    public required string Prefijo { get; init; }

    public required int IdMarca { get; init; }
}
