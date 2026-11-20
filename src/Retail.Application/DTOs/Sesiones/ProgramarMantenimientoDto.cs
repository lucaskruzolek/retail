namespace Retail.Application.DTOs.Sesiones;

/// <summary>
/// Datos de frontera para programar un mantenimiento: se validan antes de llegar a la base.
/// </summary>
public record class ProgramarMantenimientoDto
{
    public required int MinutosDesdeAhora { get; init; }
    public string? Motivo { get; init; }
}
