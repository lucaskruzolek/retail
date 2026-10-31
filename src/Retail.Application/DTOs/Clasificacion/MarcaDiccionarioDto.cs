namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Marca activa con su nombre tal como se guarda (grafía propia, "3M") y sus alias en forma de comparación.
/// </summary>
public record class MarcaDiccionarioDto
{
    public required int IdMarca { get; init; }

    public required string NombreMarca { get; init; }

    public required IReadOnlyList<string> Alias { get; init; }
}
