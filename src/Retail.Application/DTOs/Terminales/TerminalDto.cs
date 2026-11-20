namespace Retail.Application.DTOs.Terminales;

/// <summary>
/// Datos visibles de una terminal (puesto de trabajo) del local.
/// </summary>
public record class TerminalDto
{
    public required int Id { get; init; }
    public required string Nombre { get; init; }
    public required string NombreEquipo { get; init; }
}
