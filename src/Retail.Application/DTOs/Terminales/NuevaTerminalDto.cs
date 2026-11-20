namespace Retail.Application.DTOs.Terminales;

/// <summary>
/// Datos para dar de alta una terminal nueva al activar esta PC.
/// </summary>
public record class NuevaTerminalDto
{
    public required string Nombre { get; init; }
}
