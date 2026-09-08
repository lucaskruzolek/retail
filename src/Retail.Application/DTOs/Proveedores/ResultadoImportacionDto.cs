namespace Retail.Application.DTOs.Proveedores;

/// <summary>
/// Resumen del proceso de importación masiva de planillas de distribuidores.
/// </summary>
public record class ResultadoImportacionDto
{
    public required int TotalFilasProcesadas { get; init; }
    public required int PreciosActualizados { get; init; }
    public required int NuevosRegistros { get; init; }
    public required int FilasConError { get; init; }
    public required TimeSpan TiempoTranscurrido { get; init; }
}
