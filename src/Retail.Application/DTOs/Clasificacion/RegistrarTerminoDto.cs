namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Registra un término en la tabla de términos, o reasigna el que ya existe (Track 6.6, M-16, M-27). Sin categoría,
/// el término queda ambiguo: se conoce, pero por sí solo no alcanza para sugerir y el producto pasa al modelo.
/// </summary>
public record class RegistrarTerminoDto
{
    public required string Termino { get; init; }

    public int? IdCategoria { get; init; }
}
