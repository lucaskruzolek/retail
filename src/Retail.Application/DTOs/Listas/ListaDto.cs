namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Detalle de una lista de artículos con precios del catálogo de hoy.
/// </summary>
public record class ListaDto
{
    public required int IdLista { get; init; }

    public required string Nombre { get; init; }

    /// <summary>Instante UTC de la última modificación: es el valor que la pantalla devuelve como <c>FechaModificacionLeida</c>.</summary>
    public required DateTime FechaModificacion { get; init; }

    public required IReadOnlyList<RenglonListaDto> Renglones { get; init; }

    /// <summary>Suma de los subtotales de los renglones disponibles, calculada por la consulta con el precio de hoy.</summary>
    public required decimal TotalEstimado { get; init; }
}
