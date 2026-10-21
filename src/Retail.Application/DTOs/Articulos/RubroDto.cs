namespace Retail.Application.DTOs.Articulos;

/// <summary>
/// Representación de un rubro, agrupador de categorías, para su administración.
/// </summary>
public record class RubroDto
{
    public required int IdRubro { get; init; }
    public required string NombreRubro { get; init; }

    /// <summary>
    /// Categorías activas del rubro (D-1). Un rubro con categorías en uso no puede darse de baja.
    /// </summary>
    public int CantidadEnUso { get; init; }
}
