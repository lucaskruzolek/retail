namespace Retail.Application.DTOs.Fiscal;

/// <summary>
/// Resumen del proceso de reintento en lote de comprobantes en contingencia desde la Consola Fiscal.
/// </summary>
public record class ReintentoLoteResultadoDto
{
    public required int TotalProcesados { get; init; }
    public required int TotalAutorizados { get; init; }
    public required int TotalFallidos { get; init; }
    public required IReadOnlyList<ComprobanteFiscalDto> ComprobantesActualizados { get; init; }
}
