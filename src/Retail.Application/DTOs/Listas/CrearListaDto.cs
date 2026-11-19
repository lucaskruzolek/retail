namespace Retail.Application.DTOs.Listas;

/// <summary>
/// Datos para crear una lista de artículos (RF-30). El nombre debe ser único entre las listas activas (L-10).
/// </summary>
public record class CrearListaDto
{
    public required int IdUsuario { get; init; }

    public required string Nombre { get; init; }

    public required IReadOnlyList<RenglonListaEntradaDto> Renglones { get; init; }
}
