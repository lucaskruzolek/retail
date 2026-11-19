namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Nuevo contenido de una lista existente: lo usa el editor de la pestaña Listas y el "Actualizar «nombre»" del POS
/// (L-16). Reemplaza el nombre y los renglones completos.
/// </summary>
public record class ActualizarListaDto
{
    public required string Nombre { get; init; }

    public required IReadOnlyList<RenglonListaEntradaDto> Renglones { get; init; }

    /// <summary>
    /// Control optimista visible para el usuario: la <c>FechaModificacion</c> que la pantalla leyó al abrir la lista.
    /// Si la lista cambió mientras tanto, la actualización se rechaza en lugar de pisar en silencio los cambios de otra
    /// terminal que editaba la misma lista.
    /// </summary>
    public required DateTime FechaModificacionLeida { get; init; }
}
