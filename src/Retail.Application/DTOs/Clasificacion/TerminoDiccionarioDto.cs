namespace Retail.Application.DTOs.Clasificacion;

/// <summary>
/// Entrada de la tabla de términos: con categoría sugiere por sí sola; sin categoría es ambigua (M-27).
/// </summary>
public record class TerminoDiccionarioDto
{
    public required string Termino { get; init; }

    public int? IdCategoria { get; init; }
}
