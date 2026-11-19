namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Fila de la grilla de listas.
/// </summary>
public record class ListaResumenDto
{
    public required int IdLista { get; init; }

    public required string Nombre { get; init; }

    /// <summary>Cantidad de renglones cuyo artículo sigue activo.</summary>
    public required int CantidadArticulos { get; init; }

    public required decimal TotalEstimado { get; init; }

    /// <summary>Ventas activas que se originaron en esta lista; se calcula en SQL, no se guarda (L-09).</summary>
    public required int CantidadVentas { get; init; }

    public required DateTime FechaModificacion { get; init; }
}
