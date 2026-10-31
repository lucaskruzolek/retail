namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Descripción de un artículo confirmado (activo y con categoría activa) y su categoría: la materia prima de los pares
/// propuestos (Track 6.6, Fase 5, M-49).
/// </summary>
public record class DescripcionClasificadaDto
{
    public required string Descripcion { get; init; }

    public required int IdCategoria { get; init; }

    public required string NombreCategoria { get; init; }
}
